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
  constructor(private apiService: ApiService) {}

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
          this.remates = data.map(r => ({...r, expanded: false, textExpanded: false})); // Initialize expanded state
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
   * Aplica los filtros activos (tipo y búsqueda) a la lista de remates.
   * Actualiza el array filteredRemates con los resultados que coinciden con los criterios.
   * La búsqueda busca en el título, expediente y texto original del remate.
   */
  applyFilters() {
    this.filteredRemates = this.remates.filter(r => {
      // Type Filter
      const typeMatch = this.filterType === 'all' || 
                        (this.filterType === 'vehiculo' && r.tipo === 'Vehiculo') ||
                        (this.filterType === 'propiedad' && r.tipo === 'Propiedad');
      
      // Search Filter
      const q = this.searchQuery.toLowerCase();
      const searchMatch = !this.searchQuery || 
                          (r.titulo && r.titulo.toLowerCase().includes(q)) ||
                          (r.expediente && r.expediente.toLowerCase().includes(q)) ||
                          (r.textoOriginal && r.textoOriginal.toLowerCase().includes(q));
                          
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
