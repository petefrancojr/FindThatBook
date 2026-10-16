import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <main class="page">
      <h1>Search</h1>
    </main>
  `,
  styles: `
    .page {
      max-width: 720px;
      margin: 0 auto;
      padding: 4rem 1rem;
    }

    h1 {
      margin: 0;
      font-size: 2rem;
      font-weight: 600;
    }
  `,
})
export class App {}
