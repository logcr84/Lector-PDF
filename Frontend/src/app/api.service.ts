import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Remate } from './remate.model';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = 'http://localhost:5032/api/remates'; 

  constructor(private http: HttpClient) {}

  uploadPdf(file: File): Observable<Remate[]> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<Remate[]>(`${this.apiUrl}/upload`, formData);
  }
}
