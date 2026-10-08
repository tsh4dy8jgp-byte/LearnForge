import { NextStepDto } from '../api-contracts';

// The API sends reason codes; wording lives here so it can be localized later.
export function describeStep(step: NextStepDto): string {
  const objective = step.objectiveTitle ?? 'this objective';
  switch (step.reason) {
    case 'startObjective':
      return `Begin "${objective}" by reading "${step.lessonTitle}".`;
    case 'continueReading':
      return `Finish reading "${step.lessonTitle}" for "${objective}".`;
    case 'needsEvidence':
      return `Practise "${objective}" so your progress can be measured.`;
    case 'belowProficient':
      return `Keep practising "${objective}" until it becomes proficient.`;
    case 'reviewDue':
      return `Review "${objective}": it has been a while since you practised it.`;
    case 'readyForMock':
      return 'Every objective is proficient. Take a timed mock to build readiness evidence.';
    case 'assessmentAvailable':
      return 'Take an assessment to demonstrate what you know and build objective evidence.';
  }
}

export function stepLink(
  packId: string,
  step: NextStepDto,
): { path: string[]; query: Record<string, string> } {
  const path = ['/courses', packId];
  switch (step.kind) {
    case 'readLesson':
      return { path, query: { tab: 'learn', lesson: step.lessonId ?? '' } };
    case 'takeMock':
      return { path, query: { tab: 'practice', mode: 'mock', blueprint: step.blueprintId ?? '' } };
    default:
      // Objectives practised only inside case studies come with a blueprint for a balanced learning session.
      return step.blueprintId
        ? { path, query: { tab: 'practice', blueprint: step.blueprintId } }
        : { path, query: { tab: 'practice', objective: step.objectiveId ?? '' } };
  }
}

export const stepActions: Record<NextStepDto['kind'], string> = {
  readLesson: 'Open lesson',
  practise: 'Practise',
  review: 'Review',
  takeMock: 'Start a mock',
};
