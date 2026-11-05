// Unsaved answers are stored per user and attempt so a failed save survives a reload.
const prefix = (userId: string | undefined) => 'learnforge.draft.' + userId + '.';

export function draftKey(userId: string | undefined, attemptId: string | null) {
  return prefix(userId) + attemptId;
}

export function removeDrafts(userId: string | undefined) {
  for (const key of Object.keys(localStorage))
    if (key.startsWith(prefix(userId))) localStorage.removeItem(key);
}
