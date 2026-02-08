import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';

/**
 * Configuración principal de la aplicación Angular.
 * Define los proveedores globales necesarios para el funcionamiento de la aplicación.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    // Habilita la detección de cambios con coalescencia de eventos para mejor rendimiento
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Proporciona el cliente HTTP necesario para las peticiones al backend
    provideHttpClient()
  ]
};
