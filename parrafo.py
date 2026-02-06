import json
import re
import os
from bs4 import BeautifulSoup

def procesar_boletin_html(html_path, json_output_path):
    print(f"Leyendo archivo HTML: {html_path}...")
    
    with open(html_path, 'r', encoding='utf-8') as f:
        # Creamos la "sopa" (el objeto estructurado)
        soup = BeautifulSoup(f, 'html.parser')
    
    edictos = []
    
    # Estrategia: Buscar todos los párrafos <p>
    # En el HTML del boletín, cada párrafo de texto suele ser un elemento <p>
    parrafos = soup.find_all('p')
    
    buffer_texto = ""
    capturando = False
    
    for i, p in enumerate(parrafos):
        texto = p.get_text(" ", strip=True) # Obtiene el texto limpio
        
        # 1. Detectar INICIO del edicto
        if "En este Despacho" in texto:
            capturando = True
            buffer_texto = texto # Iniciamos el bloque con este párrafo
            
            # Caso especial: Si el edicto es muy corto y tiene el cierre en la misma línea
            if "publicación número:" in texto:
                edictos.append(buffer_texto)
                buffer_texto = ""
                capturando = False
            continue

        # 2. Si estamos dentro de un edicto, seguimos acumulando
        if capturando:
            # Verificamos si este párrafo es el CIERRE (la metadata)
            # El HTML suele poner la referencia en un <p> aparte justo después
            if "publicación número:" in texto or "Referencia N°" in texto:
                # Agregamos la metadata al texto principal para mantener el formato que tenías
                buffer_texto += " " + texto 
                edictos.append(buffer_texto)
                
                # Reseteamos
                buffer_texto = ""
                capturando = False
            else:
                # Si no es el cierre, es parte del contenido (un edicto de varios párrafos)
                buffer_texto += " " + texto

    print(f"Se encontraron {len(edictos)} edictos.")
    
    # Guardar en JSON
    data = [{"id": i+1, "contenido": edicto} for i, edicto in enumerate(edictos)]
    
    with open(json_output_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=4)
        
    print(f"Guardado en: {json_output_path}")

# --- Bloque principal ---
if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    # Busca archivos .html ahora
    archivos = [f for f in os.listdir(script_dir) if f.lower().endswith('.html')]
    
    if archivos:
        archivo_html = os.path.join(script_dir, archivos[0])
        nombre_json = os.path.splitext(archivos[0])[0] + ".json"
        json_path = os.path.join(script_dir, nombre_json)
        
        procesar_boletin_html(archivo_html, json_path)
    else:
        print("No se encontraron archivos .html en la carpeta.")