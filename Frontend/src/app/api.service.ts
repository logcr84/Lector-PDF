import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Remate } from './remate.model';

/**
 * Servicio de comunicación con la API del backend.
 * Gestiona las peticiones HTTP para procesar archivos PDF y obtener información de remates.
 */
@Injectable({
  providedIn: 'root'
})
export class ApiService {
  /** URL base del endpoint de la API de remates en el backend */
  private apiUrl = 'http://localhost:5033/api/remates'; 

  /**
   * Constructor del servicio.
   * @param http Cliente HTTP de Angular para realizar peticiones
   */
  constructor(private http: HttpClient) {}

  /**
   * Sube un archivo PDF al backend para su análisis y extracción de remates.
   * @param file Archivo PDF del boletín judicial a procesar
   * @returns Observable que emite un array de objetos Remate con la información extraída
   * @example
   * ```typescript
   * this.apiService.uploadPdf(file).subscribe({
   *   next: (remates) => console.log('Remates extraídos:', remates),
   *   error: (err) => console.error('Error:', err)
   * });
   * ```
   */
  uploadPdf(file: File): Observable<Remate[]> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<Remate[]>(`${this.apiUrl}/upload`, formData);
  }
}
