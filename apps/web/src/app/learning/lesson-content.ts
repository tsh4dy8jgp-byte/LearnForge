import { Component, input } from '@angular/core';
import { Lesson } from '../api-contracts';

// Shared by published lessons and private authoring previews.
@Component({
  selector: 'lf-lesson-content',
  template: `
    <h2>{{ lesson().title }}</h2>
    <p class="lead">{{ lesson().summary }}</p>
    @for (block of lesson().blocks; track $index) {
      <section [class]="'content-block ' + block.kind">
        @if (block.title) {
          <h3>{{ block.title }}</h3>
        }
        @if (block.kind === 'code') {
          <pre><code>{{ block.text }}</code></pre>
        } @else {
          <p>{{ block.text }}</p>
        }
      </section>
    }
  `,
})
export class LessonContent {
  readonly lesson = input.required<Lesson>();
}
