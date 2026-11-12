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
        } @else if (block.kind === 'table' && block.rows; as rows) {
          <!-- A wide table scrolls inside its own named, focusable region instead of widening the page. -->
          <div class="table-wrap lesson-table" role="region" tabindex="0" [attr.aria-label]="block.title || 'Table'">
            <table>
              @if (block.title) {
                <caption class="sr-only">{{ block.title }}</caption>
              }
              <thead>
                <tr>
                  @for (cell of rows[0]; track $index) {
                    <th scope="col">{{ cell }}</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (row of rows.slice(1); track $index) {
                  <tr>
                    @for (cell of row; track $index) {
                      @if ($first) {
                        <th scope="row">{{ cell }}</th>
                      } @else {
                        <td>{{ cell }}</td>
                      }
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (block.text) {
            <p>{{ block.text }}</p>
          }
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
