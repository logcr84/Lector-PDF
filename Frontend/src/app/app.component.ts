import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Remate } from './remate.model';
import { ApiService } from './api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'] // Note: angular.json points to styles.css globally, but component might look for css
})
export class AppComponent {
  title = 'Lector PDF Boletines';
  remates: Remate[] = [];
  filteredRemates: Remate[] = [];
  isLoading = false;
  error = '';
  
  filterType: 'all' | 'vehiculo' | 'propiedad' = 'all';
  searchQuery = '';

  constructor(private apiService: ApiService) {}

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
          this.error = 'Error al procesar el archivo. Asegúrese de que el Backend esté corriendo.';
          console.error(err);
          this.isLoading = false;
        }
      });
    }
  }

  toggleExpand(remate: Remate) {
    remate.expanded = !remate.expanded;
  }

  setFilter(type: 'all' | 'vehiculo' | 'propiedad') {
    this.filterType = type;
    this.applyFilters();
  }

  onSearch(event: any) {
    this.searchQuery = event.target.value;
    this.applyFilters();
  }

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
