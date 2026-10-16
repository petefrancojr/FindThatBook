import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideApiConfiguration } from '../api/generated';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    // Call the API on the same origin; nginx proxies /api to the server.
    provideApiConfiguration(''),
  ]
};
