import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { CourseCard } from '../api-contracts';
import { SiteSettingsStore } from '../site/site-settings-store';
@Component({
  imports: [RouterLink, FormsModule],
  templateUrl: './courses-page.html',
})
export class CoursesPage {
  protected readonly site = inject(SiteSettingsStore);
  private readonly api = inject(ApiClient);
  protected readonly courses = signal<CourseCard[]>([]);
  protected readonly query = signal('');
  protected readonly loaded = signal(false);
  protected readonly error = signal('');
  protected readonly filtered = computed(() =>
    this.courses().filter((c) =>
      (c.title + c.description).toLowerCase().includes(this.query().toLowerCase()),
    ),
  );
  constructor() {
    this.api
      .get<CourseCard[]>('/catalog')
      .then((c) => this.courses.set(c))
      .catch((e) => this.error.set(message(e)))
      .finally(() => this.loaded.set(true));
  }
}
