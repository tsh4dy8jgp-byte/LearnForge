"""Run after building the CLI: python -m unittest discover -s tools/flashcards -v."""
import importlib.util
import copy
import contextlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import time
import unittest
from unittest.mock import patch
import zipfile

from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("card_export", Path(__file__).with_name("export.py"))
export = importlib.util.module_from_spec(spec)
spec.loader.exec_module(export)


class ExportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="learnforge-card-tests-")
        cls.directory = Path(cls.scratch.name)
        built = cls.directory / "build"
        subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/cli"), "--no-build", "--", "build",
                        str(ROOT / "docs/examples/flashcards-sample.json"), "--out", str(built)], check=True, capture_output=True)
        pack = json.loads((built / "pack.private.json").read_text())
        delivery = json.loads((built / "delivery.json").read_text())
        cls.payload = {"id": pack["id"], "title": pack["title"], "version": pack["version"], "decks": pack["flashcardDecks"],
                       "units": delivery["flashcards"], "options": {"format": "pdf", "paper": "a4", "mode": "duplex", "binding": "long", "offsetX": 0, "offsetY": 0}}

    @classmethod
    def tearDownClass(cls):
        cls.scratch.cleanup()

    def test_apkg_models_identities_and_new_card_scheduling(self):
        identities = []
        for index in range(2):
            destination = self.directory / f"deck-{index}.apkg"
            export.export_apkg(self.payload, destination)
            with zipfile.ZipFile(destination) as archive:
                database = self.directory / f"collection-{index}.db"
                database.write_bytes(archive.read("collection.anki2"))
            with contextlib.closing(sqlite3.connect(database)) as db:
                self.assertEqual(9, db.execute("select count(*) from notes").fetchone()[0])
                self.assertEqual(11, db.execute("select count(*) from cards").fetchone()[0])
                self.assertEqual([(0, 0, 0, 0)], db.execute("select distinct type, queue, reps, lapses from cards").fetchall())
                identities.append(db.execute("select guid, mid, flds from notes order by guid").fetchall())
                types = json.loads(db.execute("select models from col").fetchone()[0])
                self.assertEqual(3, len(types))
                self.assertEqual(1, types[str(export.MODEL_IDS['cloze'])]['type'])
        self.assertEqual(identities[0], identities[1])

    def test_official_anki_import_updates_without_duplicates_or_schedule_reset(self):
        from anki.collection import Collection
        from anki import import_export_pb2 as pb
        first = self.directory / 'import-first.apkg'
        timestamp = int(time.time()) - 10
        export.export_apkg(self.payload, first, timestamp=timestamp)
        col = Collection(str(self.directory / 'anki-profile.anki2'))
        try:
            request = pb.ImportAnkiPackageRequest(package_path=str(first))
            col.import_anki_package(request); col.import_anki_package(request)
            self.assertEqual(9, col.note_count()); self.assertEqual(11, col.card_count())
            card = col.get_card(col.find_cards('')[0])
            card.queue = 2; card.type = 2; card.ivl = 42; card.reps = 4; card.due = col.sched.today + 42
            col.update_card(card)
            changed = copy.deepcopy(self.payload)
            changed['decks'][0]['notes'][0]['back'] = 'Updated answer.'
            updated = self.directory / 'import-updated.apkg'; export.export_apkg(changed, updated, timestamp=timestamp + 2)
            col.import_anki_package(pb.ImportAnkiPackageRequest(package_path=str(updated), options=pb.ImportAnkiPackageOptions(
                update_notes=pb.IMPORT_ANKI_PACKAGE_UPDATE_CONDITION_ALWAYS, with_scheduling=False)))
            self.assertEqual(9, col.note_count()); self.assertEqual(11, col.card_count())
            self.assertEqual(42, col.get_card(card.id).ivl); self.assertEqual(4, col.get_card(card.id).reps)
            self.assertTrue(any(n['Back'] == 'Updated answer.' for n in (col.get_note(i) for i in col.find_notes('')) if 'Back' in n.keys()))
            backup = self.directory / 'import-text.zip'; export.export_text(changed, backup)
            with zipfile.ZipFile(backup) as archive:
                for kind in export.MODEL_IDS:
                    file = self.directory / ('import-' + kind + '.txt'); file.write_bytes(archive.read(kind + '.txt'))
                    col.import_csv(pb.ImportCsvRequest(path=str(file), metadata=col.get_csv_metadata(str(file), None)))
            self.assertEqual(9, col.note_count()); self.assertEqual(11, col.card_count())
            self.assertEqual(42, col.get_card(card.id).ivl)
        finally:
            col.close()

    def test_text_backup_has_all_types_and_stable_first_field(self):
        destination = self.directory / "text.zip"
        export.export_text(self.payload, destination)
        with zipfile.ZipFile(destination) as archive:
            self.assertEqual({'basic.txt', 'reversible.txt', 'cloze.txt', 'README.txt', 'note-types.json'}, set(archive.namelist()))
            for kind in export.MODEL_IDS:
                text = archive.read(kind + '.txt').decode('utf-8')
                self.assertIn('#separator:Tab', text)
                self.assertIn('recall-foundations/study/', text)
                self.assertIn('#notetype:LearnForge ' + kind.title() + ' v1', text)

    def test_all_papers_modes_and_partial_sheets(self):
        import pymupdf
        for paper in ('a4', 'letter'):
            for mode in ('duplex', 'foldable'):
                payload = {**self.payload, 'options': {**self.payload['options'], 'paper': paper, 'mode': mode}}
                file = self.directory / f'{paper}-{mode}.pdf'
                export.export_pdf(payload, file)
                reader = PdfReader(file)
                card_pages = 4 if mode == 'duplex' else 3
                self.assertGreaterEqual(len(reader.pages), 2 + card_pages + 1)
                self.assertIn('Calibration', reader.pages[1].extract_text())
                texts = [reader.pages[i].extract_text() for i in range(2, 2 + card_pages)]
                for unit in payload['units']:
                    self.assertEqual(2, sum(t.count(unit['id']) for t in texts))
                # Verify identifier positions, including empty slots on the final sheet.
                document = pymupdf.open(file)
                if mode == 'duplex':
                    for start in (2, 4):
                        front, back = document[start], document[start + 1]
                        for unit in payload['units']:
                            f = front.search_for(unit['id']); b = back.search_for(unit['id'])
                            if f:
                                self.assertEqual(1, len(b))
                                self.assertAlmostEqual(f[0].y0, b[0].y0, delta=0.5)
                                self.assertAlmostEqual(abs(f[0].x0 - b[0].x0), 93 * 72 / 25.4, delta=0.5)

    def test_chinese_exports_embed_font_license_and_preserve_tone_marked_text(self):
        pack = json.loads((ROOT / 'packs/chinese-writing-pilot.json').read_text())
        notes = pack['flashcardDecks'][0]['notes']
        units = [dict(id='recognition/' + n['id'] + '/forward', front=n['front'], back=n['back'],
                      explanation=n['explanation'], language=n['language'], sources=[]) for n in notes]
        payload = {**self.payload, 'id': pack['id'], 'title': pack['title'], 'decks': pack['flashcardDecks'], 'units': units}
        font = ROOT / 'apps/web/public/writing/fonts/LearnForgeWenKaiPilot.ttf'
        with patch.object(export, 'cjk_font', return_value=font), patch.dict(os.environ, {'LEARNFORGE_CARD_FONT': ''}):
            pdf = self.directory / 'chinese.pdf'
            export.export_pdf(payload, pdf)
            text = '\n'.join(page.extract_text() for page in PdfReader(pdf).pages)
            self.assertIn('一', text); self.assertIn('茶', text); self.assertIn('yī', text)
            deck = self.directory / 'chinese.apkg'; export.export_apkg(payload, deck)
        with zipfile.ZipFile(deck) as archive:
            media = json.loads(archive.read('media'))
            self.assertIn('_LearnForgeWenKaiPilot.ttf', media.values())
            self.assertIn('_LearnForgeWenKai-OFL.txt', media.values())
            database = self.directory / 'chinese-anki.db'; database.write_bytes(archive.read('collection.anki2'))
        with sqlite3.connect(database) as db:
            self.assertEqual(30, db.execute('select count(*) from notes').fetchone()[0])
            models = json.loads(db.execute('select models from col').fetchone()[0])
            self.assertTrue(all('_LearnForgeWenKaiPilot.ttf' in model['css'] for model in models.values()))

    def test_short_edge_mapping_and_overflow(self):
        self.assertEqual([6, 7, 4, 5, 2, 3, 0, 1], [export.back_slot(i, 'short') for i in range(8)])
        payload = {**self.payload, 'units': [{**self.payload['units'][0], 'front': 'Very long answer ' * 500}]}
        with self.assertRaisesRegex(ValueError, 'does not fit'):
            export.export_pdf(payload, self.directory / 'overflow.pdf')
        payload = {**self.payload, 'units': [{**self.payload['units'][0], 'id': 'deck/' + 'n' * 120 + '/forward'}]}
        with self.assertRaisesRegex(ValueError, 'ID too long'):
            export.export_pdf(payload, self.directory / 'long-id.pdf')
        with self.assertRaisesRegex(ValueError, 'subject title'):
            export.export_pdf({**self.payload, 'title': '漢字'}, self.directory / 'title.pdf')

    def test_cyrillic_text_and_literal_markup_are_preserved(self):
        payload = copy.deepcopy(self.payload)
        unit = payload['units'][0]
        unit.update(front='Қазақ тілі: Ә, Ғ, Қ, Ң, Ө, Ұ, Ү. Русский текст < 3 & 4.',
                    back='Жауап: есте сақтау.', explanation='Пояснение и пример.', language='kk')
        payload['units'] = [unit]
        destination = self.directory / 'unicode.pdf'; export.export_pdf(payload, destination)
        text = '\n'.join(page.extract_text() for page in PdfReader(destination).pages)
        self.assertIn('Қазақ тілі', text); self.assertIn('< 3 & 4', ' '.join(text.split()))
        self.assertIn('Жауап', text)
        self.assertEqual('&lt;script&gt;&amp;', export.escaped('<script>&'))


if __name__ == '__main__':
    unittest.main()
