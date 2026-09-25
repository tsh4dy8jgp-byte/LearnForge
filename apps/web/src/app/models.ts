export interface User {
  id: string;
  displayName: string;
  email: string;
  publisher: boolean;
}
export interface Objective {
  id: string;
  title: string;
  prerequisites: string[];
}
export interface Lesson {
  id: string;
  title: string;
  summary: string;
  objectiveIds: string[];
  blocks: { kind: string; text: string; title?: string }[];
}
export interface Blueprint {
  id: string;
  title: string;
  count: number;
  minutes: number;
  size: string;
  objectiveIds: string[];
  requiredKinds: string[];
  lockSections: boolean;
}
export interface CourseCard {
  id: string;
  title: string;
  description: string;
  version: string;
  lessonCount: number;
  questionCount: number;
  objectiveCount: number;
}
export interface Course extends CourseCard {
  objectives: Objective[];
  lessons: Lesson[];
  blueprints: Blueprint[];
  sources: { title: string; url: string }[];
  completedLessons: string[];
  license: string;
}
export interface Option {
  id: string;
  text: string;
}
export interface Question {
  id: string;
  kind: string;
  prompt: string;
  objectiveIds: string[];
  options: Option[];
  slots: { id: string; text: string; options: Option[] }[];
  selectCount: number;
  reuse: boolean;
  scenarioId?: string;
  weight: number;
}
export interface Answer {
  selected: string[];
  slots: Record<string, string>;
}
export interface Grade {
  earned: number;
  possible: number;
  fullyCorrect: boolean;
  correct: string[];
  matches: Record<string, string> | null;
  explanation: string;
}
export interface AttemptSummary {
  id: string;
  packId: string;
  title: string;
  version: string;
  size: string;
  mode: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  deadline: string;
  correctPercent: number;
  score: number;
  eligible: boolean;
  freshPercent: number;
  timedOut: boolean;
  itemCount: number;
  focus: string | null;
}
export interface Attempt {
  id: string;
  packId: string;
  title: string;
  version: string;
  mode: string;
  size: string;
  status: string;
  revision: number;
  startedAt: string;
  deadline: string;
  completedAt: string | null;
  sectionIndex: number;
  sections: string[];
  lockSections: boolean;
  timedOut: boolean;
  focus: string | null;
  questions: Question[];
  scenarios: { id: string; title: string; background: string }[];
  objectives: Objective[];
  answers: Record<string, Answer>;
  feedback: Record<string, Grade>;
  results: Record<string, Grade> | null;
  serverTime: string;
  summary: {
    earned: number;
    possible: number;
    score: number;
    correctPercent: number;
    eligible: boolean;
    freshPercent: number;
  } | null;
}
export interface Readiness {
  ready: boolean;
  message: string;
  threshold: number;
  short: { required: number; streak: number; met: boolean; attemptIds: string[] };
  full: { required: number; streak: number; met: boolean; attemptIds: string[] };
}
export interface CourseProgress {
  id: string;
  title: string;
  version: string;
  lessonCount: number;
  completedLessons: number;
  readiness: Readiness;
  objectives: (Objective & {
    samples: number;
    independentSamples: number;
    correctPercent: number | null;
    lessonIds: string[];
  })[];
  recommendations: { objectiveId: string; title: string; lessonId: string; reason: string }[];
}
export interface Dashboard {
  courses: CourseProgress[];
  attempts: AttemptSummary[];
  completedAttempts: number;
  activeAttempts: number;
  completedLessons: number;
}
// Generated from the API's OpenAPI document by `make api-types`. New code uses these;
// the hand-written interfaces above are replaced page by page.
export type {
  CourseCatalogDto,
  CourseGoal,
  CourseGoalStatusDto,
  CourseProgressDto,
  DashboardDto,
  EnrollmentStatus,
  MasteryState,
  NextStepDto,
  NextStepKind,
  NextStepReason,
  ObjectiveMasteryDto,
} from './generated/types.gen';
