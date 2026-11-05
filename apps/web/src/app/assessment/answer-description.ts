import { Answer, Grade, Question } from '../api-contracts';

const typed = (q: Question) => q.kind === 'numeric' || q.kind === 'codeOutput';

export function hasResponse(answer: Answer | undefined): boolean {
  return (
    !!answer &&
    (answer.selected.length > 0 || Object.keys(answer.slots).length > 0 || !!answer.text?.trim())
  );
}

// Selection kinds are shown by option text; typed kinds show what the learner entered.
export function describeAnswer(q: Question, answer?: Answer): string {
  if (!answer) return 'Unanswered';
  if (typed(q)) return answer.text?.trim() ? answer.text : 'Unanswered';
  if (q.kind === 'single' || q.kind === 'multiple' || q.kind === 'sequence')
    return (
      answer.selected
        .map((id) => q.options.find((o) => o.id === id)?.text || id)
        .join(q.kind === 'sequence' ? ' → ' : ', ') || 'Unanswered'
    );
  return q.slots
    .map(
      (s) =>
        s.text +
        ': ' +
        ((q.kind === 'matching' ? q.options : s.options).find((o) => o.id === answer.slots[s.id])
          ?.text || 'Unanswered'),
    )
    .join(' · ');
}

// Typed kinds may accept several values; any of them earns the mark.
export function describeExpected(q: Question, grade: Grade): string {
  if (typed(q)) return grade.correct.join(' or ');
  return describeAnswer(q, { selected: grade.correct, slots: grade.matches || {} });
}
