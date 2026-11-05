import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CourseCatalogDto } from '../../api-contracts';
import { LessonContent } from '../../learning/lesson-content';

@Component({
  selector: 'lf-course-lessons',
  imports: [RouterLink, LessonContent],
  templateUrl: './course-lessons.html',
})
export class CourseLessons {
  readonly course = input.required<CourseCatalogDto>();
  readonly lessonId = input.required<string>();
  readonly completed = input.required<Set<string>>();
  readonly revised = input.required<Set<string>>();
  readonly signedIn = input.required<boolean>();
  readonly busy = input(false);
  readonly selectLesson = output<string>();
  readonly completeLesson = output<string>();
  protected readonly current = computed(
    () =>
      this.course().lessons.find((lesson) => lesson.id === this.lessonId()) ??
      this.course().lessons[0],
  );
}
