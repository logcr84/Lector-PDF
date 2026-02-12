import { Pipe, PipeTransform } from '@angular/core';
import { DatePipe } from '@angular/common';

@Pipe({
  name: 'customDate',
  standalone: true
})
export class CustomDatePipe implements PipeTransform {

  constructor() {}

  transform(value: string | null | undefined, format: string = 'dd MMM'): string {
    if (!value) return '';

    // If value matches dd/MM/yyyy HH:mm format
    // Regex for dd/MM/yyyy (time optional)
    const dateRegex = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s+(\d{1,2}):(\d{1,2}))?/;
    const match = value.match(dateRegex);

    if (match) {
      const day = parseInt(match[1], 10);
      const month = parseInt(match[2], 10) - 1; // Month is 0-indexed
      const year = parseInt(match[3], 10);
      const hour = match[4] ? parseInt(match[4], 10) : 0;
      const minute = match[5] ? parseInt(match[5], 10) : 0;

      const dateObj = new Date(year, month, day, hour, minute);
      
      // Use Angular's DatePipe logic or custom formatting
      // Simple custom formatting to avoid importing DatePipe provider complexity if not needed
      // But using DatePipe is easier for 'dd MMM' format
      
      const datePipe = new DatePipe('en-US'); 
      // Note: Locale 'en-US' or 'es-ES' depends on app config. 
      // Assuming 'es' based on HTML lang='es', but default pipe might employ en-US if not configured.
      
      try {
          return datePipe.transform(dateObj, format) || value;
      } catch (e) {
          return value;
      }
    }
    
    // If it's already a valid ISO string or Date object string, try standar parsing
    const timestamp = Date.parse(value);
    if (!isNaN(timestamp)) {
        const datePipe = new DatePipe('en-US');
        return datePipe.transform(value, format) || value;
    }

    // If parsing fails, return original string (it might be text like "Ver texto")
    return value;
  }
}
