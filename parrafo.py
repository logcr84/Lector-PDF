import re
import json
import sys
from pypdf import PdfReader

def extraer_parrafos_despacho(pdf_path, json_output_path):
    """
    Extrae los párrafos que inician con 'En este Despacho' de un Boletín Judicial
    y los guarda en un archivo JSON.
    
    Mejoras:
    - Extracción de texto con mejor separación de palabras
    - Patrón regex flexible para manejar variaciones de formato y OCR
    """
    print(f"Leyendo archivo: {pdf_path}...")
    
    try:
        reader = PdfReader(pdf_path)
        full_text = ""
        
        # 1. Extraer texto de todas las páginas con mejor separación
        # Similar a como lo hace PdfParserService.cs con GetWords()
        for page in reader.pages:
            # Extraer con modo layout para mejor preservación de espacios
            text = page.extract_text(extraction_mode="layout")
            if text:
                # Asegurar que hay espacios entre palabras
                # Normalizar múltiples espacios a uno solo
                text = re.sub(r' +', ' ', text)
                full_text += text + "\n"
        
        # 2. Normalizar espacios (eliminar cortes de línea arbitrarios del PDF)
        # Esto convierte el texto en una sola línea continua para facilitar la búsqueda
        clean_text = re.sub(r'\s+', ' ', full_text)
        
        # 3. Definir el patrón de búsqueda - MÁS FLEXIBLE
        # Mejoras en el patrón:
        # - Coma opcional después de "Despacho"
        # - Acepta variaciones de acentos (ó/o, ú/u) comunes en OCR
        # - Dos puntos opcionales después de "número"
        # - Espacios flexibles
        patron = r"En este Despacho[,\s]+.*?publicaci[óo]n n[úu]mero\s*:?\s*\d+\s+de\s+\d+"
        
        matches = re.findall(patron, clean_text, re.IGNORECASE)
        
        print(f"Se encontraron {len(matches)} párrafos.")
        
        # 4. Estructurar datos para JSON
        data = [{"id": i+1, "contenido": match.strip()} for i, match in enumerate(matches)]
        
        # 5. Guardar en JSON
        with open(json_output_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=4)
            
        print(f"Datos guardados exitosamente en: {json_output_path}")

    except Exception as e:
        print(f"Error al procesar el archivo: {e}")

if __name__ == "__main__":
    import os
    
    # Obtener el directorio donde se encuentra el script
    script_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Buscar archivos PDF en el directorio
    pdf_files = [f for f in os.listdir(script_dir) if f.lower().endswith('.pdf')]
    
    if pdf_files:
        # Tomar el primer PDF encontrado
        archivo_pdf = os.path.join(script_dir, pdf_files[0])
        # Use simple string replacement or splitext to swap extension
        nombre_base = os.path.splitext(pdf_files[0])[0]
        archivo_json = os.path.join(script_dir, f"{nombre_base}.json")
        
        print(f"Archivo PDF detectado: {pdf_files[0]}")
        extraer_parrafos_despacho(archivo_pdf, archivo_json)
    else:
        print("No se encontraron archivos PDF en la carpeta del script.")