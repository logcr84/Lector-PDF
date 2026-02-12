import re

text = """En este Despacho Con una base de TREINTA Y SEIS MILLONES OCHOCIENTOS MIL COLONES EXACTOS, libre de gravámenes y anotaciones; sáquese a remate la finca del partido de HEREDIA, matrícula 256578. DERECHO: 001 y 002. NATURALEZA: TERRENO PARA CONSTRUIR. SITUADA EN EL DISTRITO 5-SANTA LUCÍA. CANTÓN 2-BARVA DE LA PROVINCIA DE HEREDIA. FINCA SE ENCUENTRA EN ZONA CATASTRADA. LINDEROS: NORTE: CALLE PUBLICA CON UN FRENTE DE VEINTIÚN PUNTO SESENTA Y SEIS METROS. SUR: PRADOS AZULES DEL ORIENTE S.A. ESTE: PRADOS AZULES DEL ORIENTE S.A. OESTE: FRANCISCO ESQUIVEL VILLALOBOS Y RAFAEL ESQUIVEL VILLALOBOS. MIDE: CUATROCIENTOS CINCUENTA METROS CUADRADOS. PLANO: H-2022681-2017. Para tal efecto, se señalan las catorce horas treinta minutos del veinte de julio de dos mil veintiséis. De no haber postores, el segundo remate se efectuará a las catorce horas treinta minutos del veintiocho de julio de dos mil veintiséis con la base de VEINTISIETE MILLONES SEISCIENTOS MIL COLONES EXACTOS (75% de la base original) y de continuar sin oferentes, para el tercer remate se señalan las catorce horas treinta minutos del cinco de agosto de dos mil veintiséis con la base de NUEVE MILLONES DOSCIENTOS MIL COLONES EXACTOS (25% de la base original). NOTAS: Se le informa a las personas interesadas en participar en la almoneda que en caso de pagar con cheque certificado, el mismo deberá ser emitido a favor de este despacho. Publíquese este edicto dos veces consecutivas, la primera publicación con un mínimo de cinco días de antelación a la fecha fijada para la subasta. Se remata por ordenarse así en PROCESO EJECUCIÓN HIPOTECARIA de GRUPO MUTUAL ALAJUELA - LA VIVIENDA DE AHORRO Y PRESTAMO contra HENRY ARTURO GOMEZ BOLAÑOS EXP:24-010146-1158- CJ JUZGADO DE COBRO DE HEREDIA. Hora y fecha de emisión: dieciséis horas con veintiséis minutos del doce de enero del dos mil veintiséis. Lic. Pedro Ubau Hernández, Juez Tramitador."""

# Regex for amounts: UPPERCASE words ending with COLONES EXACTOS
# We use a lookahead or just match enough context.
# [A-ZÁÉÍÓÚÑ]+(\s+[A-ZÁÉÍÓÚÑ]+)* COLONES EXACTOS
amount_pattern = r'([A-ZÁÉÍÓÚÑ]+(?:\s+[A-ZÁÉÍÓÚÑ]+)*) COLONES EXACTOS'

# Regex for dates: "del <day> de <month> de <year>"
# Day: one or more words (e.g. "veinte", "veintiún", "treinta y uno")
# Month: specific list or just word
# Year: "dos mil" followed by word
date_pattern = r'(?:del|el)\s+([a-zñáéíóú]+(?:\s+y\s+[a-zñáéíóú]+)?)\s+de\s+([a-z]+)\s+d?e\s+(dos\s+mil\s+[a-zñáéíóú]+)'

amounts = [m.strip() + " COLONES EXACTOS" for m in re.findall(amount_pattern, text)]
dates_tuples = re.findall(date_pattern, text, re.IGNORECASE)
dates = [f"{d[0]} de {d[1]} de {d[2]}" for d in dates_tuples]

print("--- Extracción ---")
print(f"Total Montos encontrados: {len(amounts)}")
print(f"Total Fechas encontradas: {len(dates)}")

print("\n--- Montos ---")
for a in amounts:
    print(a)

print("\n--- Fechas ---")
for d in dates:
    print(d)

print("\n--- Pares Sugeridos ---")
# Logic: 
# Date 1 corresponds to Amount 1
# Date 2 corresponds to Amount 2
# Date 3 corresponds to Amount 3
# Provided they exist.
max_len = min(len(dates), len(amounts))
# Wait, let's check the logic.
# Text: Amount1 (Base) ... Date1 ... Date2 ... Amount2 ... Date3 ... Amount3 ... Date4 (Emission)
# So simply zipping might be wrong if the order is A, D, D, A, D, A.
# Let's verify positions.

print("Analizando posiciones...")
amount_iter = re.finditer(amount_pattern, text)
date_iter = re.finditer(date_pattern, text, re.IGNORECASE)

events = []
for m in amount_iter:
    events.append({'type': 'monto', 'text': m.group(1).strip() + " COLONES EXACTOS", 'pos': m.start()})
for m in date_iter:
    d = m.groups()
    events.append({'type': 'fecha', 'text': f"{d[0]} de {d[1]} de {d[2]}", 'pos': m.start()})

events.sort(key=lambda x: x['pos'])

current_base = None
remates = []

# Manual logic based on expected "Remate" flow
# Usually: Base -> Remate 1 -> Remate 2 (Base 75%) -> Remate 3 (Base 25%)
# In this text:
# ... base de AMOUNT1 ... señalan ... DATE1.
# ... segundo remate ... DATE2 ... base de AMOUNT2.
# ... tercer remate ... DATE3 ... base de AMOUNT3.

# So:
# 1. AMOUNT1 is active. DATE1 found. Pair (DATE1, AMOUNT1).
# 2. DATE2 found. Wait, where is the amount? It comes AFTER.
# 3. AMOUNT2 found.
# 4. DATE3 found.
# 5. AMOUNT3 found.

# Final pairing logic:
# - If we see a Date, look for the NEAREST Amount.
# - Valid Auctions usually define the base either before or immediately after.
# Let's iterate and try to pair.

processed_events = []
# We know the specific structure of this legal text (Boletin Judicial).
# 1st Remate: Base (before) -> Date
# 2nd Remate: Date -> Base (after)
# 3rd Remate: Date -> Base (after)

# Let's just output the list fully first as that's what was asked ("extraigas todas las fechas... van fecha y monto").
# Maybe "van fecha y monto" means the user wants the output format to be "Date, Amount".

# Heuristic:
# 1. Find the first amount. That's the main base.
# 2. Find the first date. That's the 1st auction. Pair it with Main Base.
# 3. Find subsequent date/amount pairs.

# Let's construct the output.
final_pairs = []

# List of all extracted amounts and dates in order
print("Secuencia encontrada:")
for e in events:
    print(f"[{e['type'].upper()}] {e['text']}")

# Building the pairs for user
# We can try to match them up based on the index.
# We expect 3 dates for auctions.
auction_dates = [e for e in events if e['type'] == 'fecha'][:3] # The last date is emission date.
auction_amounts = [e for e in events if e['type'] == 'monto']

if len(auction_dates) >= 1 and len(auction_amounts) >= 1:
    final_pairs.append((auction_dates[0]['text'], auction_amounts[0]['text']))

if len(auction_dates) >= 2 and len(auction_amounts) >= 2:
    final_pairs.append((auction_dates[1]['text'], auction_amounts[1]['text']))
    
if len(auction_dates) >= 3 and len(auction_amounts) >= 3:
    final_pairs.append((auction_dates[2]['text'], auction_amounts[2]['text']))

print("\n--- Resultado Final (Formato: Fecha | Monto) ---")
for d, a in final_pairs:
    print(f"{d} | {a}")
