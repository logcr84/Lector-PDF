/**
 * Punto de entrada principal de la aplicación Angular.
 * Inicializa Zone.js para la detección de cambios y arranca el componente raíz con su configuración.
 */
import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

// Arranca la aplicación con el componente raíz y su configuración
bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
