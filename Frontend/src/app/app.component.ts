import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Remate } from './remate.model';
import { ApiService } from './api.service';
import { CustomDatePipe } from './custom-date.pipe';

/**
 * Componente principal de la aplicación Lector PDF Boletines.
 * Gestiona la carga de archivos PDF, extracción de remates y filtrado de resultados.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, CustomDatePipe],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'] // Note: angular.json points to styles.css globally, but component might look for css
})
export class AppComponent {
  /** Título de la aplicación */
  title = 'Lector PDF Boletines';

  /** Array completo de remates extraídos del PDF */
  remates: Remate[] = [];

  /** Array de remates filtrados según los criterios de búsqueda y tipo */
  filteredRemates: Remate[] = [];

  /** Indicador de carga mientras se procesa el archivo PDF */
  isLoading = false;

  /** Mensaje de error para mostrar al usuario */
  error = '';

  /** Tipo de filtro activo: todos, vehículos o propiedades */
  filterType: 'all' | 'vehiculo' | 'propiedad' = 'all';

  /** Texto de búsqueda ingresado por el usuario */
  searchQuery = '';

  /** Controla la visibilidad del modal de tabla */
  showModal = false;

  /**
   * Constructor del componente.
   * @param apiService Servicio de comunicación con la API del backend
   */
  constructor(private apiService: ApiService) { }

  /**
   * Alterna la visibilidad del modal de tabla.
   */
  toggleModal() {
    this.showModal = !this.showModal;
  }

  /**
   * Maneja la selección de archivo PDF por parte del usuario.
   * Envía el archivo al backend para su procesamiento y actualiza la lista de remates.
   * Gestiona estados de carga y errores durante el proceso.
   * @param event Evento de cambio del input de archivo
   */
  onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      this.isLoading = true;
      this.error = '';
      this.apiService.uploadPdf(file).subscribe({
        next: (data) => {
          this.remates = data.map(r => ({ ...r, expanded: false, textExpanded: false })); // Initialize expanded state
          this.applyFilters();
          this.isLoading = false;
        },
        error: (err) => {
          console.error('Upload error:', err);

          // Extract error message from backend response
          if (err.error && err.error.message) {
            this.error = err.error.message;
            if (err.error.details) {
              this.error += ' ' + err.error.details;
            }
          } else if (err.status === 400) {
            this.error = 'El archivo no contiene datos válidos de remates. Verifica que sea un boletín oficial.';
          } else {
            this.error = 'Error al procesar el archivo. Asegúrese de que el Backend esté corriendo.';
          }

          this.isLoading = false;
        }
      });
    }
  }

  /**
   * Alterna el estado de expansión de un remate en la interfaz.
   * Permite mostrar u ocultar los detalles completos del remate.
   * @param remate Objeto remate cuyo estado de expansión se desea cambiar
   */
  toggleExpand(remate: Remate) {
    remate.expanded = !remate.expanded;
  }

  /**
   * Establece el filtro por tipo de bien (todos, vehículo o propiedad).
   * Aplica automáticamente los filtros para actualizar la vista.
   * @param type Tipo de filtro a aplicar
   */
  setFilter(type: 'all' | 'vehiculo' | 'propiedad') {
    this.filterType = type;
    this.applyFilters();
  }

  /**
   * Maneja el evento de búsqueda de texto.
   * Actualiza la consulta de búsqueda y aplica los filtros.
   * @param event Evento de input con el texto de búsqueda
   */
  onSearch(event: any) {
    this.searchQuery = event.target.value;
    this.applyFilters();
  }

  /**
   * Mapa de sinónimos para expansión de búsqueda.
   * Permite encontrar resultados relacionados aunque no coincidan exactamente con el texto.
   */
  readonly synonyms: { [key: string]: string[] } = {
    'carro': ['vehiculo', 'automovil', 'auto'],
    'coche': ['vehiculo', 'automovil'],
    'camioneta': ['vehiculo'],
    'moto': ['motocicleta', 'vehiculo'],
    'lote': ['finca', 'terreno', 'propiedad'],
    'casa': ['finca', 'vivienda', 'propiedad'],
    'terreno': ['finca', 'lote', 'propiedad']
  };

  /**
   * Calcula la distancia de Levenshtein entre dos cadenas.
   * Útil para detectar errores tipográficos leves (fuzzy matching).
   * @param a Primera cadena
   * @param b Segunda cadena
   * @returns Número de ediciones necesarias para transformar a en b
   */
  private getLevenshteinDistance(a: string, b: string): number {
    if (a.length === 0) return b.length;
    if (b.length === 0) return a.length;

    const matrix = [];

    // Increment along the first column of each row
    for (let i = 0; i <= b.length; i++) {
      matrix[i] = [i];
    }

    // Increment each column in the first row
    for (let j = 0; j <= a.length; j++) {
      matrix[0][j] = j;
    }

    // Fill in the rest of the matrix
    for (let i = 1; i <= b.length; i++) {
      for (let j = 1; j <= a.length; j++) {
        if (b.charAt(i - 1) == a.charAt(j - 1)) {
          matrix[i][j] = matrix[i - 1][j - 1];
        } else {
          matrix[i][j] = Math.min(matrix[i - 1][j - 1] + 1, // substitution
            Math.min(matrix[i][j - 1] + 1, // insertion
              matrix[i - 1][j] + 1)); // deletion
        }
      }
    }

    return matrix[b.length][a.length];
  }

  /**
   * Realiza una búsqueda inteligente verificando si un remate coincide con los términos de búsqueda.
   * Soporta coincidencia exacta, parcial, sinónimos y fuzzy matching.
   * @param item El remate a evaluar
   * @param query La consulta de búsqueda completa
   * @returns true si el remate es relevante para la búsqueda
   */
  private smartMatch(item: Remate, query: string): boolean {
    if (!query) return true;

    const terms = query.toLowerCase().split(/\s+/).filter(t => t.length > 0);
    const itemText = (
      (item.titulo || '') + ' ' +
      (item.expediente || '') + ' ' +
      (item.textoOriginal || '') + ' ' +
      (item.tipo || '') + ' ' +
      Object.values(item.detalles || {}).join(' ')
    ).toLowerCase();

    // EVERY search term must match something in the item (AND logic)
    return terms.every(term => {
      // 1. Exact or Substring match (fastest)
      if (itemText.includes(term)) return true;

      // 2. Synonym expansion
      if (this.synonyms[term]) {
        if (this.synonyms[term].some(syn => itemText.includes(syn))) return true;
      }

      // 3. Fuzzy matching (slowest, only for terms > 3 chars to avoid noise)
      // Check against specific key fields if implementation allows, or scan words in text
      if (term.length > 3) {
        // Optimization: split item text into words and check distance
        // This is heavy, so we limit the search scope or optimize
        const words = itemText.split(/\s+/);
        // Allow max 1 edit for short words (4-5 chars), 2 for longer
        const maxDist = term.length > 5 ? 2 : 1;

        return words.some(w => {
          if (Math.abs(w.length - term.length) > maxDist) return false;
          return this.getLevenshteinDistance(term, w) <= maxDist;
        });
      }

      return false;
    });
  }

  /**
   * Aplica los filtros activos (tipo y búsqueda) a la lista de remates.
   * Actualiza el array filteredRemates con los resultados que coinciden con los criterios.
   * Utiliza la lógica de smartMatch para búsquedas más flexibles.
   */
  applyFilters() {
    this.filteredRemates = this.remates.filter(r => {
      // Type Filter
      const typeMatch = this.filterType === 'all' ||
        (this.filterType === 'vehiculo' && r.tipo === 'Vehiculo') ||
        (this.filterType === 'propiedad' && r.tipo === 'Propiedad');

      // Smart Search Filter
      const searchMatch = this.smartMatch(r, this.searchQuery);

      return typeMatch && searchMatch;
    });
  }

  /**
   * Helper to parse "dd/MM/yyyy HH:mm" string to Date object
   */
  private parseDate(dateStr: string): Date | null {
    if (!dateStr) return null;
    try {
      // Expected format: "dd/MM/yyyy HH:mm" or "dd/MM/yyyy"
      const parts = dateStr.split(' ');
      const dateParts = parts[0].split('/');
      const timeParts = parts[1] ? parts[1].split(':') : ['00', '00'];

      if (dateParts.length !== 3) return null;

      return new Date(
        parseInt(dateParts[2]),
        parseInt(dateParts[1]) - 1,
        parseInt(dateParts[0]),
        parseInt(timeParts[0]),
        parseInt(timeParts[1])
      );
    } catch (e) {
      console.error('Date parse error', e);
      return null;
    }
  }

  /**
   * Determines the status of a specific auction date (passed, active, future).
   * @param remate The specific date entry
   * @param allRemates List of all dates for this item (to determine order)
   */
  getDateStatus(remate: any, allRemates: any[]): 'passed' | 'active' | 'future' {
    const date = this.parseDate(remate.fecha);
    if (!date) return 'future';

    const now = new Date();

    // If date is in the past
    if (date < now) {
      return 'passed';
    }

    // It is in the future. Check if it's the *first* future date.
    // If any *previous* date (by index) is also future, then this one is just 'future' (not active yet).
    // Note: This assumes allRemates is sorted by date. If not, we might need to sort or finding min future date.
    // Assuming backend sends them chronological: 1st, 2nd, 3rd.

    const index = allRemates.indexOf(remate);
    if (index > 0) {
      // Check if previous one is future
      const prevDate = this.parseDate(allRemates[index - 1].fecha);
      if (prevDate && prevDate >= now) {
        return 'future';
      }
    }

    return 'active';
  }

  // Wrapper for template strictly for "isNext" check
  isNextDate(remate: any, allRemates: any[]): boolean {
    return this.getDateStatus(remate, allRemates) === 'active';
  }

  // Wrapper for template strictly for "isPassed" check
  isDatePassed(remate: any): boolean {
    const date = this.parseDate(remate.fecha);
    return date ? date < new Date() : false;
  }
}
