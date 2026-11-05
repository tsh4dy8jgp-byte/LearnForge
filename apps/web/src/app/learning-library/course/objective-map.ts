import { Component, input, output } from '@angular/core';
import { CourseCatalogDto, ObjectiveMasteryDto } from '../../api-contracts';
import { MasteryBadge } from '../../learning/mastery-badge';

@Component({
  selector: 'lf-objective-map',
  imports: [MasteryBadge],
  templateUrl: './objective-map.html',
})
export class ObjectiveMap {
  readonly course = input.required<CourseCatalogDto>();
  readonly mastery = input.required<Map<string, ObjectiveMasteryDto>>();
  readonly signedIn = input.required<boolean>();
  readonly openLesson = output<string>();
  readonly practise = output<string>();
  protected prerequisiteNames(ids: string[]) {
    return ids
      .map((id) => this.course().objectives.find((objective) => objective.id === id)?.title ?? id)
      .join(', ');
  }
}
