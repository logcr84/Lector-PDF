import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Remate } from './remate.model';
import { ApiService } from './api.service';

/**
 * Componente principal de la aplicación Lector PDF Boletines.
 * Gestiona la carga de archivos PDF, extracción de remates y filtrado de resultados.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
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
          this.remates = data.map(r => ({...r, expanded: false})); // Initialize expanded state
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
}
