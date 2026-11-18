import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ApiStatusService } from './core/api-status';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly api = inject(ApiStatusService);

  constructor() {
    this.api.check();
  }
}
