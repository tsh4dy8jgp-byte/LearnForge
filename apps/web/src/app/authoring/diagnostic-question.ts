import { AuthoringPreviewDto } from '../api-contracts';

// Prefer the longest question ID because IDs may themselves contain dots.
export function questionFor(report: AuthoringPreviewDto | null, path: string) {
  return (report?.questions ?? [])
    .filter((question) => path === question.id || path.startsWith(question.id + '.'))
    .sort((a, b) => b.id.length - a.id.length)[0]?.id;
}
