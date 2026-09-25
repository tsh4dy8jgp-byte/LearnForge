# Migration guide

The University and AI-103/AB-100 repositories were reviewed as design references and remain unchanged. Their content was not bulk copied.

| Existing material | LearnForge |
| --- | --- |
| Course/unit/module | Pack, lesson and objective |
| Learning outcome | Objective |
| Reading/example/misconception | Lesson blocks |
| Question/key | Typed question and grading |
| Exam mode | Blueprint plus mock/learn mode |
| Case study | Scenario and grouped questions |
| Attempt history | Immutable snapshot |
| Focus/review queue | Focused learning session |
| Citation | HTTPS SourceReference |
| Completion | User/pack/lesson progress with a content hash (survives new releases) |

## Import process

1. Freeze a source revision and inventory licenses.
2. Extract objectives and stable IDs.
3. Convert teaching material to plain text blocks.
4. Convert questions to one supported kind and assign family IDs.
5. Link each lesson and question to an objective.
6. Add explanations for misconceptions.
7. Define short/full blueprints and test feasibility.
8. Run the CLI checker and inspect private artifacts.
9. Have a subject expert cross-check keys, explanations and sources.
10. Publish a new version after a browser smoke test.

Do not translate provider-specific wording mechanically. Remove secrets, tokens, user records and proprietary keys. Do not imply that imported material is official exam content.
