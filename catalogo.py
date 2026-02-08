import json
import re
import math
import os
import glob
from datetime import datetime

# ==========================================
# 1. SPANISH TEXT TO NUMBER CONVERTER
# ==========================================
def text_to_number(text):
    """
    Parses a Spanish phrase representing a number/currency and returns a float.
    Examples: 
      "OCHO MIL CIENTO VEINTISIETE DÓLARES CON VEINTIÚN CENTAVOS"
      "SIETE MILLONES DE COLONES"
    """
    text = text.upper().replace(" Y ", " ").replace(" CON ", " ")
    
    # Currency detection
    currency = "CRC"
    if "DÓLARES" in text or "DOLARES" in text:
        currency = "USD"
    
    # Remove currency words to parse just the number
    text = re.sub(r'DÓLARES|DOLARES|COLONES|EXACTOS|CENTAVOS|CÉNTIMOS', '', text).strip()

    # Mapping words to values
    # Note: This is a simplified parser. For full production robustness, 
    # specific libraries exist, but this handles the common legal format.
    values = {
        'UN': 1, 'UNO': 1, 'UNA': 1,   'DOS': 2, 'TRES': 3, 'CUATRO': 4, 'CINCO': 5,
        'SEIS': 6, 'SIETE': 7, 'OCHO': 8, 'NUEVE': 9, 'DIEZ': 10,
        'ONCE': 11, 'DOCE': 12, 'TRECE': 13, 'CATORCE': 14, 'QUINCE': 15,
        'DIECISEIS': 16, 'DIECISÉIS': 16, 'DIECISIETE': 17, 'DIECIOCHO': 18, 'DIECINUEVE': 19,
        'VEINTE': 20, 'VEINTI': 20, 'VEINTIUN': 21, 'VEINTIÚN': 21, 'VEINTIDOS': 22, 'VEINTIDÓS': 22,
        'VEINTITRES': 23, 'VEINTITRÉS': 23, 'VEINTICUATRO': 24, 'VEINTICINCO': 25, 'VEINTISEIS': 26, 'VEINTISÉIS': 26,
        'VEINTISIETE': 27, 'VEINTIOCHO': 28, 'VEINTINUEVE': 29,
        'TREINTA': 30, 'CUARENTA': 40, 'CINCUENTA': 50, 'SESENTA': 60, 'SETENTA': 70, 'OCHENTA': 80, 'NOVENTA': 90,
        'CIEN': 100, 'CIENTO': 100, 'DOSCIENTOS': 200, 'TRESCIENTOS': 300, 'CUATROCIENTOS': 400, 'QUINIENTOS': 500,
        'SEISCIENTOS': 600, 'SETECIENTOS': 700, 'OCHOCIENTOS': 800, 'NOVECIENTOS': 900,
        'MIL': 1000, 'MILLON': 1000000, 'MILLONES': 1000000
    }

    # Tokenize
    tokens = text.split()
    total_value = 0
    current_value = 0
    
    for token in tokens:
        val = values.get(token, 0)
        
        if val == 1000:
            if current_value == 0: current_value = 1
            current_value *= 1000
            total_value += current_value
            current_value = 0
        elif val >= 1000000:
            if current_value == 0: current_value = 1
            current_value *= val
            total_value += current_value
            current_value = 0
        else:
            current_value += val
            
    total_value += current_value
    
    # Handle cents (roughly) - in legal text usually follows "CON XX CENTAVOS"
    # Since we cleaned "CON" and "CENTAVOS", the cents remain at the end of the text stream 
    # but the logic above adds them as integers.
    # To fix this properly without a complex parser, let's look for the original "CON" split
    # This is a heuristic.
    
    return total_value, currency

def extract_price_and_currency_robust(text_segment):
    """
    Extracts number and currency from a specific segment like "base de X... (75%...)"
    """
    # Simply looking for the text before "COLONES" or "DÓLARES"
    match = re.search(r'base de (.*?) (COLONES|DÓLARES|DOLARES)', text_segment, re.IGNORECASE)
    if match:
        amount_text = match.group(1)
        # Parse cents if explicitly mentioned to division logic
        cents_match = re.search(r'CON (.*?) (CENTAVOS|CÉNTIMOS)', text_segment, re.IGNORECASE)
        
        val, curr = text_to_number(amount_text + (" " + match.group(2)))
        
        cents = 0
        if cents_match:
            c_val, _ = text_to_number(cents_match.group(1))
            cents = c_val
            
        final_val = val + (cents / 100.0)
        return final_val, curr
    return 0, "CRC"

# ==========================================
# 2. PARSING LOGIC
# ==========================================

def parse_auction_entry(entry):
    content = entry['contenido']
    
    # -- 1. Determine Type --
    # Property keywords
    if re.search(r'\bfinca\b|\bterreno\b|\blote\b|\bpropiedad\b', content, re.IGNORECASE) and not re.search(r'\bvehículo\b|\bcarro\b|\bmoto\b|\bchasis\b', content, re.IGNORECASE):
        item_type = 'propiedad'
    else:
        item_type = 'vehiculo'

    # -- 2. Prices & Dates --
    # We look for 1st, 2nd, 3rd auction patterns
    # 1st: "señalan las [HORA] del [FECHA]" ... "base de [PRECIO]" (The first price is at the start "Con una base de...")
    # 2nd: "segundo remate se efectuará... base de [PRECIO]"
    # 3rd: "tercer remate se señalan... base de [PRECIO]"
    
    remates = []
    
    # Base Price (1st Auction)
    price1, currency = extract_price_and_currency_robust(content[:300]) # Look near start
    
    # Date 1st Auction
    date1_match = re.search(r'señalan las .*? del (.*?) de (dos mil .*?)\.', content)
    date1 = date1_match.group(0).replace("señalan las ", "") if date1_match else "No indicado"

    remates.append({
        "label": "Primer Remate",
        "price": price1,
        "date": date1,
        "currency": currency
    })
    
    # 2nd Auction
    match_2nd = re.search(r'segundo remate se efectuará (.*?) \(75%', content)
    if match_2nd:
        seg_text = match_2nd.group(1)
        # Extract date
        d2 = re.search(r'a las .*? dos mil .*? con', seg_text)
        date2 = d2.group(0).replace("a las ", "").replace("con", "") if d2 else "No indicado"
        
        # Extract price
        p2_text = re.search(r'base de (.*)', seg_text)
        price2 = 0
        if p2_text:
            p2, _ = text_to_number(p2_text.group(1))
            # Rough fix for cents logic in text_to_number for sub-clauses
            # If the number seems huge, it might have added cents as ints. 
            # For safe measure, we can just calculate 75% of base
            price2 = price1 * 0.75
            
        remates.append({
            "label": "Segundo Remate",
            "price": price2,
            "date": date2,
            "currency": currency
        })
        
    # 3rd Auction
    match_3rd = re.search(r'tercer remate se señalan (.*?) \(25%', content)
    if match_3rd:
        ter_text = match_3rd.group(1)
        # Extract date
        d3 = re.search(r' las .*? dos mil .*? con', ter_text)
        date3 = d3.group(0).replace(" las ", "").replace("con", "") if d3 else "No indicado"
        
        # Price
        price3 = price1 * 0.25 # Calc directly for safety
        
        remates.append({
            "label": "Tercer Remate",
            "price": price3,
            "date": date3,
            "currency": currency
        })

    # -- 3. Title & Specs --
    title = ""
    area = "--"
    
    details = {}
    
    if item_type == 'propiedad':
        # Area
        area_match = re.search(r'MIDE: (.*?)(\.|PLANO)', content)
        if area_match:
            area_text = area_match.group(1)
            # Try to extract numbers
            # Usually in words "CIENTO CINCUENTA..."
            # For title display, let's try to parse or just truncate
            # If we want numeric area for sorting/display:
            try:
                area_val, _ = text_to_number(area_text)
                area = f"{int(area_val)} m²"
            except:
                area = "Ver detalle"
        
        # Location (Title)
        loc_match = re.search(r'Situada en (.*?)(, de la provincia|\.)', content, re.IGNORECASE)
        if loc_match:
            title = loc_match.group(1).strip()
        else:
            title = "Propiedad Sin Ubicación Identificada"
            
        # Extract Details for box
        details['Naturaleza'] = re.search(r'es (.*?) Situada', content).group(1) if re.search(r'es (.*?) Situada', content) else ""
        details['Colindantes'] = re.search(r'COLINDA: (.*?) MIDE', content).group(1) if re.search(r'COLINDA: (.*?) MIDE', content) else ""
        
    else:
        # Vehicle Title
        marca = re.search(r'Marca:? (.*?)(,|\.)', content)
        estilo = re.search(r'Estilo:? (.*?)(,|\.)', content)
        anio = re.search(r'Año:? (.*?)(,|\.)', content)
        
        m_str = marca.group(1) if marca else "Vehículo"
        e_str = estilo.group(1) if estilo else ""
        a_str = f"({anio.group(1)})" if anio else ""
        
        title = f"{m_str} {e_str} {a_str}".strip()
        
        # Details
        details['Placa'] = re.search(r'(Placa|placas) (.*?)(,)', content).group(2) if re.search(r'(Placa|placas) (.*?)(,)', content) else ""
        details['Motor'] = re.search(r'Motor:? (.*?)(,|\.)', content).group(1) if re.search(r'Motor:? (.*?)(,|\.)', content) else ""
        
    # Legal Info Common
    details['Expediente'] = re.search(r'EXP:(.*?)(JUZGADO| )', content).group(1) if re.search(r'EXP:(.*?)(JUZGADO| )', content) else ""
    details['Juzgado'] = re.search(r'JUZGADO (.*?)( \.| $)', content).group(1) if re.search(r'JUZGADO (.*?)( \.| $)', content) else ""
    details['Demandante'] = re.search(r' de (.*?) contra ', content).group(1) if re.search(r' de (.*?) contra ', content) else ""
    details['Demandado'] = re.search(r' contra (.*?) EXP', content).group(1) if re.search(r' contra (.*?) EXP', content) else ""

    return {
        "id": entry['id'],
        "type": item_type,
        "title": title,
        "area": area,
        "remates": remates,
        "details": details,
        "raw": content
    }

# ==========================================
# 3. HTML GENERATION
# ==========================================

def formatted_currency(value, currency):
    symbol = "₡" if currency == "CRC" else "$"
    return f"{symbol}{value:,.2f}"

def generate_html(parsed_data, output_file):
    
    items_html = ""
    
    for item in parsed_data:
        # Determine CSS class
        css_class = "prop-item" if item['type'] == 'propiedad' else "car-item"
        badge_class = "bg-prop" if item['type'] == 'propiedad' else "bg-car"
        badge_text = "Propiedad" if item['type'] == 'propiedad' else "Vehículo"
        
        # Price Display (1st auction)
        main_price = formatted_currency(item['remates'][0]['price'], item['remates'][0]['currency'])
        
        # Expediente Info
        expediente_val = item['details'].get('Expediente', 'No indicado')
        expediente_html = f'<div class="expediente-info"><strong>EXP:</strong> {expediente_val}</div>'
        
        # Remates Cards HTML
        remates_cards = ""
        for r in item['remates']:
            p_fmt = formatted_currency(r['price'], r['currency'])
            # Clean date formatting slightly
            d_fmt = r['date'].replace("horas", "").replace("minutos", "").strip()
            # Capitalize First Letters
            d_fmt = d_fmt.title() 
            
            remates_cards += f"""
                    <div class="remate-card">
                        <span class="remate-label">{r['label']}</span>
                        <span class="remate-price">{p_fmt}</span>
                        <span class="remate-date">{d_fmt}</span>
                    </div>
            """
            
        # Details HTML
        details_rows = ""
        for k, v in item['details'].items():
            if v and len(v) < 200: # Simple filter for length
                details_rows += f'<div class="detail-row"><span class="detail-key">{k}:</span> <span class="detail-value">{v}</span></div>'

        # Accordion HTML Construction
        items_html += f"""
        <!-- ITEM {item['id']} -->
        <button class="accordion {css_class}" data-type="{item['type']}">
            <div class="header-grid">
                <span class="h-title">{item['title']}</span>
                <span class="h-area">{item['area']}</span>
                <span class="h-price">{main_price}</span>
            </div>
        </button>
        <div class="panel {item['type']}-panel">
            <div class="panel-content">
                <span class="badge {badge_class}">{badge_text}</span>
                <div class="remates-grid">
                    {remates_cards}
                </div>
                {expediente_html}
                <div class="details-box">
                    <div class="section-title">Detalles & Legal</div>
                    {details_rows}
                    <div style="margin-top:10px; font-size: 0.8em; color: #999;">
                        <em>Texto original extraído: {item['raw'][:100]}...</em>
                    </div>
                </div>
            </div>
        </div>
        """

    # Full HTML Template
    # reusing the CSS from provided info.html
    html_template = f"""<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Boletín Oficial de Remates - Generado</title>
    <style>
        :root {{
            --prop-primary: #2c3e50;
            --prop-light: #ecf0f1;
            --car-primary: #d35400;
            --car-light: #fbeee6;
            --text-main: #333;
            --text-muted: #7f8c8d;
            --bg-body: #f4f6f7;
            --card-shadow: 0 2px 5px rgba(0, 0, 0, 0.08);
        }}

        body {{
            font-family: 'Segoe UI', Helvetica, Arial, sans-serif;
            background-color: var(--bg-body);
            color: var(--text-main);
            margin: 0;
            padding: 30px;
            line-height: 1.5;
        }}

        .container {{
            max-width: 1000px;
            margin: 0 auto;
        }}
        
        /* CONTROLS AREA */
        .controls {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            background: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: var(--card-shadow);
            flex-wrap: wrap;
            gap: 10px;
        }}
        
        .filter-btn {{
            padding: 8px 15px;
            border: 1px solid #ddd;
            background: #f8f9fa;
            cursor: pointer;
            border-radius: 4px;
            font-weight: 600;
            transition: 0.2s;
        }}
        
        .filter-btn:hover, .filter-btn.active {{
            background: var(--prop-primary);
            color: white;
            border-color: var(--prop-primary);
        }}

        .search-box {{
            padding: 8px 12px;
            border: 1px solid #ddd;
            border-radius: 4px;
            width: 250px;
            font-size: 14px;
        }}
        
        .pagination {{
            display: flex;
            gap: 5px;
            align-items: center;
        }}
        
        .page-btn {{
            padding: 8px 12px;
            background: white;
            border: 1px solid #ddd;
            cursor: pointer;
            border-radius: 4px;
        }}

        .page-btn.active {{
            background: var(--prop-primary);
            color: white;
            border-color: var(--prop-primary);
        }}

        h1 {{
            text-align: center;
            color: var(--prop-primary);
            margin-bottom: 30px;
            font-weight: 300;
            text-transform: uppercase;
            letter-spacing: 2px;
            border-bottom: 1px solid #ddd;
            padding-bottom: 15px;
        }}

        /* Estilos del Acordeón (Encabezado) */
        .accordion {{
            background-color: #fff;
            cursor: pointer;
            padding: 15px 20px;
            width: 100%;
            border: none;
            text-align: left;
            outline: none;
            font-size: 16px;
            transition: 0.3s;
            margin-bottom: 5px;
            border-radius: 6px;
            box-shadow: var(--card-shadow);
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-left: 6px solid transparent;
        }}

        .prop-item {{ border-left-color: var(--prop-primary); }}
        .car-item {{ border-left-color: var(--car-primary); }}

        .accordion:hover, .accordion.active {{ background-color: #fafafa; }}

        /* Layout del Encabezado */
        .header-grid {{
            display: grid;
            grid-template-columns: 3fr 1fr 1.2fr;
            width: 100%;
            gap: 15px;
            align-items: center;
        }}

        .h-title {{ 
            font-weight: 600; 
            color: #444; 
            font-size: 0.95em;
            /* Truncate long titles */
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            display: block;
        }}
        .h-area {{ text-align: center; color: var(--text-muted); font-size: 0.9em; }}
        .h-price {{ text-align: right; font-weight: 700; font-size: 1.1em; color: #27ae60; }}

        /* Panel de Contenido Desplegable */
        .panel {{
            padding: 0 20px;
            background-color: #fff;
            max-height: 0;
            overflow: hidden;
            transition: max-height 0.3s ease-out;
            margin-bottom: 10px;
            border-radius: 0 0 6px 6px;
            border: 1px solid #eee;
            border-top: none;
            display: none; /* Hidden by default for logic control */
        }}
        
        .panel.show {{
            display: block;
            /* max-height handled by JS */
        }}

        .panel-content {{ padding: 25px 0; }}

        /* Etiquetas */
        .badge {{
            display: inline-block;
            padding: 5px 10px;
            border-radius: 4px;
            font-size: 0.75em;
            font-weight: bold;
            color: #fff;
            text-transform: uppercase;
            margin-bottom: 15px;
            letter-spacing: 0.5px;
        }}
        .bg-prop {{ background-color: var(--prop-primary); }}
        .bg-car {{ background-color: var(--car-primary); }}

        /* Tarjetas Remates */
        .remates-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }}
        .remate-card {{
            background-color: #f8f9fa;
            border: 1px solid #e1e4e8;
            border-radius: 6px;
            padding: 15px;
            text-align: center;
        }}
        .remate-label {{ display: block; font-size: 0.75em; color: var(--text-muted); text-transform: uppercase; font-weight: 600; margin-bottom: 5px; }}
        .remate-price {{ display: block; font-size: 1.1em; font-weight: bold; color: var(--text-main); margin-bottom: 8px; }}
        .remate-date {{ font-size: 0.85em; color: #555; line-height: 1.3; }}

        .expediente-info {{
            text-align: center;
            margin-bottom: 20px;
            font-size: 1.1em;
            color: var(--prop-primary);
            background: #eef2f5;
            padding: 10px;
            border-radius: 4px;
            border: 1px dashed #ccc;
        }}

        /* Detalles */
        .details-box {{
            background-color: #fcfcfc;
            border-left: 3px solid #ddd;
            padding: 15px;
            font-size: 0.9em;
            color: #555;
        }}
        .detail-row {{ display: flex; margin-bottom: 6px; border-bottom: 1px solid #eee; padding-bottom: 4px; }}
        .detail-row:last-child {{ border-bottom: none; }}
        .detail-key {{ font-weight: 600; width: 130px; flex-shrink: 0; color: var(--prop-primary); }}
        .detail-value {{ flex-grow: 1; }}
        .section-title {{
            font-weight: 700; color: var(--prop-primary); margin-bottom: 10px;
            text-transform: uppercase; font-size: 0.8em; letter-spacing: 0.5px;
            border-bottom: 2px solid #eee; padding-bottom: 3px;
        }}
        
        .hidden {{ display: none !important; }}

        @media (max-width: 768px) {{
            .header-grid {{ grid-template-columns: 1fr; gap: 5px; }}
            .h-area, .h-price {{ text-align: left; }}
            .h-price {{ margin-top: 5px; }}
            .controls {{ flex-direction: column; align-items: stretch; }}
            .pagination {{ justify-content: center; margin-top: 10px; }}
        }}
    </style>
</head>
<body>

    <div class="container">
        <h1>Listado Oficial de Remates</h1>
        
        <div class="controls">
            <div>
                <button class="filter-btn active" onclick="filterItems('all')">Todos</button>
                <button class="filter-btn" onclick="filterItems('propiedad')">Propiedades</button>
                <button class="filter-btn" onclick="filterItems('vehiculo')">Vehículos</button>
            </div>
            <div>
                 <input type="text" id="searchInput" class="search-box" placeholder="Buscar expediente, título o contenido..." onkeyup="filterItems()">
            </div>
            <div class="pagination" id="paginationControls">
                <!-- JS will inject buttons here -->
            </div>
        </div>

        <div id="itemsContainer">
            {items_html}
        </div>
    </div>

    <script>
        // === STATE ===
        const itemsPerPage = 10;
        let currentPage = 1;
        let currentFilter = 'all';
        
        // === ELEMENTS ===
        const container = document.getElementById('itemsContainer');
        const allAccordions = Array.from(container.querySelectorAll('.accordion'));
        // Pairs of (accordion, panel)
        // We handle them as atomic units logic-wise
        // The HTML structure is Button + Div.panel
        
        // === LOGIC ===
        
        function init() {{
            render();
            setupAccordionListeners();
        }}
        
        function setupAccordionListeners() {{
            allAccordions.forEach(acc => {{
                acc.addEventListener("click", function() {{
                    this.classList.toggle("active");
                    const panel = this.nextElementSibling;
                    
                    if (panel.style.maxHeight) {{
                        panel.style.maxHeight = null;
                        setTimeout(() => panel.classList.remove('show'), 300); // Wait for transition
                    }} else {{
                        panel.classList.add('show');
                        // Use timeout to allow display:block to render before calculating height
                        setTimeout(() => {{
                            panel.style.maxHeight = panel.scrollHeight + "px";
                        }}, 10);
                    }}
                }});
            }});
        }}

        function filterItems(type) {{
            currentFilter = type;
            currentPage = 1;
            
            // Update buttons styling
            document.querySelectorAll('.filter-btn').forEach(btn => {{
                if(btn.innerText.toLowerCase().includes(type == 'all' ? 'todos' : type.substring(0,4))) 
                    btn.classList.add('active');
                else 
                    btn.classList.remove('active');
            }});
            
            render();
        }}
        
        function changePage(page) {{
            currentPage = page;
            render();
            // Scroll to top of list
            document.querySelector('.controls').scrollIntoView({{behavior: 'smooth'}});
        }}

        function render() {{
            // 1. Filter Content
            let visibleItems = [];
            
            // Group items (Button + Panel)
            for(let i=0; i < allAccordions.length; i++) {{
                const btn = allAccordions[i];
                const panel = btn.nextElementSibling;
                const type = btn.getAttribute('data-type');
                
                // Search Logic
                const searchText = document.getElementById('searchInput').value.toLowerCase();
                // We search in the button text (Title, Area, Price) and the panel text (Details, Raw)
                const contentText = (btn.innerText + " " + panel.innerText).toLowerCase();
                const matchesSearch = !searchText || contentText.includes(searchText);

                if ((currentFilter === 'all' || type === currentFilter) && matchesSearch) {{
                    visibleItems.push({{btn, panel}});
                }} else {{
                    // Hide completely
                    btn.classList.add('hidden');
                    panel.classList.add('hidden');
                }}
            }}
            
            // 2. Pagination Logic
            const totalPages = Math.ceil(visibleItems.length / itemsPerPage);
            if (currentPage > totalPages) currentPage = Math.max(1, totalPages);
            
            const startIdx = (currentPage - 1) * itemsPerPage;
            const endIdx = startIdx + itemsPerPage;
            
            // Show/Hide based on page
            visibleItems.forEach((item, index) => {{
                if (index >= startIdx && index < endIdx) {{
                    item.btn.classList.remove('hidden');
                    // Panel follows button state (collapsed/expanded), but ensure it's not hidden by pag
                    item.panel.classList.remove('hidden'); 
                    // However, we only show panel content if active. 
                    // This creates a conflict if we used 'hidden' for both filtering and pagination.
                    // Let's rely on 'hidden' for filtering AND pagination filtering.
                }} else {{
                    item.btn.classList.add('hidden');
                    item.panel.classList.add('hidden');
                }}
            }});
            
            // 3. Render Pagination Controls
            const pagContainer = document.getElementById('paginationControls');
            pagContainer.innerHTML = '';
            
            // Prev
            const prevBtn = document.createElement('button');
            prevBtn.innerText = '«';
            prevBtn.className = 'page-btn';
            prevBtn.onclick = () => changePage(currentPage - 1);
            if(currentPage === 1) prevBtn.disabled = true;
            pagContainer.appendChild(prevBtn);
            
            // Numbered Buttons (Simple range for now)
            // Show max 5 buttons
            let startPage = Math.max(1, currentPage - 2);
            let endPage = Math.min(totalPages, startPage + 4);
            
            if (endPage - startPage < 4) {{
                startPage = Math.max(1, endPage - 4);
            }}
            
            for(let i=startPage; i<=endPage; i++) {{
                const btn = document.createElement('button');
                btn.innerText = i;
                btn.className = `page-btn ${{i === currentPage ? 'active' : ''}}`;
                btn.onclick = () => changePage(i);
                pagContainer.appendChild(btn);
            }}
            
            // Next
            const nextBtn = document.createElement('button');
            nextBtn.innerText = '»';
            nextBtn.className = 'page-btn';
            nextBtn.onclick = () => changePage(currentPage + 1);
            if(currentPage === totalPages || totalPages === 0) nextBtn.disabled = true;
            pagContainer.appendChild(nextBtn);
            
            // Info text
            const info = document.createElement('span');
            info.style.marginLeft = '10px';
            info.style.fontSize = '0.9em';
            info.innerText = `Pág ${{currentPage}} de ${{totalPages || 1}}`;
            pagContainer.appendChild(info);
        }}
        
        // Start
        init();

    </script>
</body>
</html>
"""
    
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(html_template)
    
    print(f"Generado exitosamente: {output_file}")


# ==========================================
# MAIN EXECUTION
# ==========================================
if __name__ == "__main__":
    # Find JSON files in current directory
    json_files = glob.glob("*.json")
    
    if not json_files:
        print("Error: No se encontraron archivos JSON en el directorio actual.")
    else:
        # Use the first one found, or prioritized logic if needed
        # (Assuming the user wants the script to just work with 'the' json)
        input_json = json_files[0]
        print(f"Archivo JSON detectado: {input_json}")
        
        # Derive output filename from input
        nombre_base = os.path.splitext(input_json)[0]
        output_html = f"{nombre_base}.html"
        
        print(f"Leyendo {input_json}...")
        try:
            with open(input_json, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            print(f"Procesando {len(data)} entradas...")
            parsed_items = [parse_auction_entry(item) for item in data]
            
            print("Generando HTML...")
            generate_html(parsed_items, output_html)
            
        except Exception as e:
            print(f"Error procesando {input_json}: {e}")
