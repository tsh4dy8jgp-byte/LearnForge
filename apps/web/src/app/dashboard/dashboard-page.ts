import { Session } from '../authentication/session';
import { Component, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { message } from '../http/api-error';
import { CourseGoal, DashboardDto } from '../api-contracts';
import { MasteryBadge } from '../learning/mastery-badge';
import { NextSteps } from '../learning/next-steps';

const goalLabels: Record<CourseGoal, string> = {
  readiness: 'EXAM READINESS',
  mastery: 'MASTERY GOAL',
  completion: 'COMPLETION GOAL',
};

@Component({
  imports: [RouterLink, DecimalPipe, DatePipe, MasteryBadge, NextSteps],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  protected readonly session = inject(Session);
  protected readonly dashboard = httpResource<DashboardDto>(() => '/api/me/dashboard');
  protected readonly message = message;
  protected goalLabel(goal: CourseGoal) {
    return goalLabels[goal];
  }
}
