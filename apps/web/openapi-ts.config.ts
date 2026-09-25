import { defineConfig } from '@hey-api/openapi-ts';

// Types only: the app keeps its own HTTP layer (the Api service and httpResource).
export default defineConfig({
  input: './openapi/learnforge.json',
  output: './src/app/generated',
  plugins: ['@hey-api/typescript'],
});
