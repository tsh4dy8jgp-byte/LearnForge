import { Component, computed, input, linkedSignal, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Answer, AuthoringPreviewDto } from '../api-contracts';
import { LessonContent } from '../learning/lesson-content';
import { QuestionInput } from '../assessment/question-input';

@Component({
  selector: 'lf-learner-preview',
  imports: [FormsModule, LessonContent, QuestionInput],
  host: { style: 'display: contents' },
  templateUrl: './learner-preview.html',
})
export class LearnerPreview {
  readonly report = input<AuthoringPreviewDto | null>(null);
  readonly questionId = model.required<string>();
  protected readonly lessonId = linkedSignal(() => this.report()?.catalog?.lessons[0]?.id ?? '');
  protected readonly previewLesson = computed(() =>
    this.report()?.catalog?.lessons.find((lesson) => lesson.id === this.lessonId()),
  );
  protected readonly previewQuestion = computed(() =>
    this.report()?.questions.find((question) => question.id === this.questionId()),
  );
  protected readonly previewScenario = computed(() =>
    this.report()?.scenarios.find((scenario) => scenario.id === this.previewQuestion()?.scenarioId),
  );
  protected readonly previewAnswer = linkedSignal<Answer>(() => {
    this.previewQuestion();
    return { selected: [], slots: {} };
  });
}
