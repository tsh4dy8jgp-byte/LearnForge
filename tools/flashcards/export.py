"""Bounded, stdin-only exporter. All content arrives from the validated Core projection."""
import csv
import hashlib
import html
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import zipfile

MODEL_IDS = {"basic": 1987422101, "reversible": 1987422102, "cloze": 1987422103}
CSS = ".card { font-family: sans-serif; font-size: 20px; text-align: left; line-height: 1.5; } .extra {font-size: 16px;}"


def escaped(text):
    return html.escape(text or "").replace("\n", "<br/>")


def models():
    import genanki
    result = {}
    for kind, identifier in MODEL_IDS.items():
        cloze = kind == "cloze"
        fields = ["LearnForgeId", "Text", "Extra", "Sources"] if cloze else ["LearnForgeId", "Front", "Back", "Extra", "Sources"]
        templates = [{"name": "Cloze", "qfmt": "{{cloze:Text}}", "afmt": "{{cloze:Text}}<hr><div class=extra>{{Extra}}<br>{{Sources}}</div>"}] if cloze else [
            {"name": "Forward", "qfmt": "{{Front}}", "afmt": "{{FrontSide}}<hr id=answer>{{Back}}<hr><div class=extra>{{Extra}}<br>{{Sources}}</div>"}]
        if kind == "reversible":
            templates.append({"name": "Reverse", "qfmt": "{{Back}}", "afmt": "{{FrontSide}}<hr id=answer>{{Front}}<hr><div class=extra>{{Extra}}<br>{{Sources}}</div>"})
        result[kind] = genanki.Model(identifier, "LearnForge " + kind.title() + " v1", fields=[{"name": f} for f in fields],
                                     templates=templates, css=CSS, model_type=genanki.Model.CLOZE if cloze else genanki.Model.FRONT_BACK)
    return result


def note_data(payload, deck, note):
    identity = payload["id"] + "/" + deck["id"] + "/" + note["id"]
    sources = "<br>".join(escaped(s["title"]) + ': <a href="' + html.escape(s["url"], quote=True) + '">' + escaped(s["url"]) + "</a>" for s in note.get("sources") or [])
    content = [escaped(note["text"]), escaped(note["explanation"]), sources] if note["kind"] == "cloze" else [escaped(note["front"]), escaped(note["back"]), escaped(note["explanation"]), sources]
    tags = ["subject::" + payload["id"], "deck::" + deck["id"], "language::" + note["language"]]
    tags += ["objective::" + o for o in note["objectiveIds"]] + (note.get("tags") or [])
    return identity, [identity] + content, tags


def export_apkg(payload, destination, timestamp=None):
    import genanki
    types = models()
    decks = []
    for deck in payload["decks"]:
        # 53-bit positive IDs remain exact in JavaScript consumers of the Anki database.
        deck_id = int.from_bytes(hashlib.sha256(("learnforge/v1/" + payload["id"] + "/" + deck["id"]).encode()).digest()[:6], "big")
        target = genanki.Deck(deck_id, "LearnForge::" + payload["title"].replace("::", "-") + "::" + deck["title"].replace("::", "-"))
        for note in deck["notes"]:
            identity, fields, tags = note_data(payload, deck, note)
            target.add_note(genanki.Note(model=types[note["kind"]], fields=fields, tags=tags, guid=genanki.guid_for(identity)))
        decks.append(target)
    # genanki's SQLite scratch database must stay inside the request directory for cleanup.
    previous_tempdir = tempfile.tempdir
    with tempfile.TemporaryDirectory(prefix='anki-', dir=destination.parent) as scratch:
        try:
            tempfile.tempdir = scratch
            genanki.Package(decks).write_to_file(str(destination), timestamp=timestamp)
        finally:
            tempfile.tempdir = previous_tempdir


def export_text(payload, destination):
    types = models()
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for kind, model in types.items():
            stream = io.StringIO(newline="")
            stream.write("#separator:Tab\n#html:true\n#notetype:" + model.name + "\n")
            stream.write("#tags column:" + str(len(model.fields) + 1) + "\n#deck column:" + str(len(model.fields) + 2) + "\n")
            writer = csv.writer(stream, delimiter="\t", lineterminator="\n")
            count = 0
            for deck in payload["decks"]:
                for note in deck["notes"]:
                    if note["kind"] == kind:
                        _, fields, tags = note_data(payload, deck, note)
                        writer.writerow(fields + [" ".join(tags), "LearnForge::" + payload["title"].replace("::", "-") + "::" + deck["title"].replace("::", "-")])
                        count += 1
            if count:
                archive.writestr(kind + ".txt", stream.getvalue().encode("utf-8"))
        specifications = {k: {"name": m.name, "fields": [f["name"] for f in m.fields], "templates": m.templates, "css": m.css} for k, m in types.items()}
        archive.writestr("note-types.json", json.dumps(specifications, ensure_ascii=False, indent=2))
        archive.writestr("README.txt", "Anki text backup (UTF-8, tab-separated, HTML enabled)\n\n"
            "Use Anki Desktop File > Import for each .txt file. These files require the named LearnForge v1 note types.\n"
            "Import the accompanying .apkg first to install the types, or create them in Tools > Manage Note Types using note-types.json.\n"
            "Create Cloze from Anki's Cloze type; create Basic/Reversible from Basic. Preserve field and template order.\n"
            "Map regular columns to the listed fields; special columns contain tags and deck names. The first field is LearnForgeId.\n"
            "Choose Update existing notes, with match scope Note type. Do not change LearnForgeId. Existing scheduling is preserved.\n"
            "The .apkg is the preferred portable import. Anki and LearnForge review schedules are independent.\n"
            "Deleting notes from an exported deck does not delete previously imported notes in Anki.\n")


def back_slot(index, binding):
    row, col = divmod(index, 2)
    return row * 2 + (1 - col) if binding == "long" else (3 - row) * 2 + col


def export_pdf(payload, destination):
    from reportlab.lib.pagesizes import A4, letter
    from reportlab.lib.styles import ParagraphStyle
    from reportlab.lib.units import mm
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.pdfgen import canvas
    from reportlab.platypus import Paragraph

    candidates = [os.environ.get("LEARNFORGE_CARD_FONT", ""), "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/Library/Fonts/Roboto-Regular.ttf"]
    font_path = next((p for p in candidates if p and Path(p).is_file()), None)
    if not font_path:
        raise ValueError("No card font found. Install DejaVu Sans or set LEARNFORGE_CARD_FONT to a Unicode .ttf file.")
    pdfmetrics.registerFont(TTFont("Cards", font_path))
    widths = pdfmetrics.getFont("Cards").face.charWidths
    for unit in payload["units"]:
        for text in [unit["front"], unit["back"], unit["explanation"]] + [s["title"] + s["url"] for s in unit["sources"]]:
            if any(ord(c) not in widths for c in text if not c.isspace()):
                raise ValueError("Card " + unit["id"] + " contains glyphs unsupported by the configured font.")
    options = payload["options"]
    width, height = A4 if options["paper"] == "a4" else letter
    card_w, card_h, gap = 90 * mm, 60 * mm, 3 * mm
    style = ParagraphStyle("card", fontName="Cards", fontSize=11, leading=14, spaceAfter=0, splitLongWords=True)
    body_style = ParagraphStyle("body", fontName="Cards", fontSize=10, leading=14)

    def paragraph(text, w, max_h, identity):
        p = Paragraph(escaped(text), style)
        _, h = p.wrap(w, max_h)
        if h > max_h:
            raise ValueError("Card " + identity + " does not fit at 11 pt. Shorten or split its front/back text.")
        return p, h

    # Complete preflight before writing any PDF. Never clip text or silently drop cards.
    for unit in payload["units"]:
        paragraph(unit["front"], card_w - 10 * mm, card_h - 16 * mm, unit["id"])
        paragraph(unit["back"], card_w - 10 * mm, card_h - 16 * mm, unit["id"])
    c = canvas.Canvas(str(destination), pagesize=(width, height), pageCompression=1)
    c.setTitle(payload["title"] + " - printable flashcards " + payload["version"])
    c.setAuthor("LearnForge")

    def heading(text):
        c.setFont("Cards", 15); c.drawString(12 * mm, height - 18 * mm, text)

    def body(text, y):
        p = Paragraph(escaped(text), body_style); _, h = p.wrap(width - 24 * mm, height)
        p.drawOn(c, 12 * mm, y - h); return y - h - 5 * mm

    heading("Printing instructions - exclude pages 1 and 2")
    y = body(payload["title"] + " / release " + payload["version"], height - 28 * mm)
    y = body("Print at Actual size / 100%. Disable Fit to page. Finished cards are 90 x 60 mm. Solid marks indicate cuts; dashed lines indicate folds.", y)
    if options["mode"] == "duplex":
        y = body("Use portrait duplex printing, flip on the " + options["binding"] + " edge. Print the card-page range only, in front/back pairs. Do not duplex these instructions with the first card sheet.", y)
    else:
        y = body("Print card pages single-sided. Cut around each 180 x 60 mm pair, fold along the middle dashed line, then attach the two halves back-to-back.", y)
    y = body("Check matching card IDs on both sides before cutting. Try one sheet first; adjust back-page X/Y offsets if your printer shifts the reverse side. Calibration is page 2; references follow the cards.", y)
    c.showPage()
    heading("Calibration - print this page separately at 100%")
    c.setLineWidth(0.5); c.rect(20 * mm, height - 110 * mm, 90 * mm, 60 * mm)
    body("The rectangle must measure exactly 90 x 60 mm. The horizontal rule below is 100 mm. Printer feed/alignment varies; compare a test front/back sheet before printing the full deck.", height - 125 * mm)
    c.line(20 * mm, height - 180 * mm, 120 * mm, height - 180 * mm)
    c.showPage()

    def card(unit, field, x, y):
        p, h = paragraph(unit[field], card_w - 10 * mm, card_h - 16 * mm, unit["id"])
        p.drawOn(c, x + 5 * mm, y + card_h - 8 * mm - h)
        c.setFont("Cards", 7)
        c.drawString(x + 5 * mm, y + 4 * mm, unit["id"])

    def crops(x, y, w, h):
        c.setLineWidth(0.3)
        for px in (x, x + w):
            for py in (y, y + h):
                c.line(px - 1 * mm, py, px + 1 * mm, py)
                c.line(px, py - 1 * mm, px, py + 1 * mm)

    def footer(text):
        c.setFont("Cards", 7); c.drawString(12 * mm, 5 * mm, text)

    per_sheet = 8 if options["mode"] == "duplex" else 4
    sheets = (len(payload["units"]) + per_sheet - 1) // per_sheet
    card_pages = sheets * (2 if options["mode"] == "duplex" else 1)
    left = (width - (2 * card_w + gap if options["mode"] == "duplex" else 2 * card_w)) / 2
    bottom = (height - (4 * card_h + 3 * gap)) / 2
    for sheet in range(sheets):
        units = payload["units"][sheet * per_sheet:(sheet + 1) * per_sheet]
        if options["mode"] == "duplex":
            for side in ("front", "back"):
                for i, unit in enumerate(units):
                    slot = i if side == "front" else back_slot(i, options["binding"])
                    row, col = divmod(slot, 2)
                    x = left + col * (card_w + gap) + (options["offsetX"] * mm if side == "back" else 0)
                    y = bottom + (3 - row) * (card_h + gap) + (options["offsetY"] * mm if side == "back" else 0)
                    card(unit, side, x, y); crops(x, y, card_w, card_h)
                footer("Sheet " + str(sheet + 1) + " / " + side + " - cards on PDF pages 3-" + str(2 + card_pages))
                c.showPage()
        else:
            for row, unit in enumerate(units):
                y = bottom + (3 - row) * (card_h + gap)
                card(unit, "front", left, y); card(unit, "back", left + card_w, y)
                crops(left, y, 2 * card_w, card_h)
                c.setDash(3, 3); c.line(left + card_w, y, left + card_w, y + card_h); c.setDash()
            footer("Foldable sheet " + str(sheet + 1) + " - cards on PDF pages 3-" + str(2 + card_pages)); c.showPage()

    heading("Explanations and references - exclude from card printing")
    y = height - 28 * mm
    for unit in payload["units"]:
        text = unit["id"] + "\n" + unit["explanation"] + "\n" + "\n".join(s["title"] + ": " + s["url"] for s in unit["sources"])
        remaining = [Paragraph(escaped(text), body_style)]
        while remaining:
            p = remaining.pop(0); _, h = p.wrap(width - 24 * mm, y - 15 * mm)
            if h <= y - 15 * mm:
                p.drawOn(c, 12 * mm, y - h); y -= h + 5 * mm
            else:
                pieces = p.split(width - 24 * mm, y - 15 * mm)
                if pieces:
                    first = pieces.pop(0); _, h = first.wrap(width - 24 * mm, y - 15 * mm)
                    first.drawOn(c, 12 * mm, y - h); remaining = pieces + remaining
                else:
                    remaining.insert(0, p)
                c.showPage(); y = height - 20 * mm
    c.save()


def main():
    payload = json.load(sys.stdin)
    destination = Path(sys.argv[1])
    {"apkg": export_apkg, "text": export_text, "pdf": export_pdf}[payload["options"]["format"]](payload, destination)


if __name__ == "__main__":
    try:
        main()
    except ValueError as error:
        print("EXPORT_ERROR:" + str(error), file=sys.stderr); sys.exit(2)
    except Exception:
        print("EXPORT_ERROR:Export failed. Check Python dependencies and configured fonts.", file=sys.stderr); sys.exit(1)
