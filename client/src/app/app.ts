import { Component } from '@angular/core';
import { Search } from './search/search';

@Component({
  selector: 'app-root',
  imports: [Search],
  template: `<app-search />`,
})
export class App {}
