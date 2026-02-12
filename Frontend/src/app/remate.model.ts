/**
 * Representa una fecha de remate con su información asociada.
 */
export interface RemateFecha {
    /** Etiqueta descriptiva del remate (ej: "1° Remate", "2° Remate") */
    label: string;
    /** Precio del remate como valor numérico */
    precio: number;
    /** Precio formateado para mostrar (incluye símbolo de moneda) */
    precioDisplay: string;
    /** Fecha y hora del remate en formato dd/MM/yyyy HH:mm */
    fecha: string;
}

/**
 * Modelo de datos que representa un remate judicial extraído de un boletín.
 * Contiene información completa del proceso de remate incluyendo fechas, precios y detalles del bien.
 */
export interface Remate {
    /** Identificador único del remate */
    id: number;
    /** Tipo de bien a rematar */
    tipo: string; // "Vehiculo" | "Propiedad"
    /** Título descriptivo del remate (ubicación o modelo del bien) */
    titulo: string;
    /** Área del inmueble (solo para propiedades) */
    area: string;
    /** Precio base del remate como valor numérico */
    precioBase: number;
    /** Precio base formateado para mostrar (incluye símbolo de moneda) */
    precioBaseDisplay: string;
    /** Lista de fechas de remates programados (típicamente 3: 100%, 75%, 25% del precio base) */
    remates: RemateFecha[];
    /** Número de expediente judicial */
    expediente: string;
    /** Detalles adicionales del bien (marca, modelo, placa, ubicación, etc.) */
    detalles: { [key: string]: string };
    /** Texto original completo extraído del boletín */
    textoOriginal: string;
    /** Nombre del demandado en el proceso judicial */
    demandado: string;
    /** Juzgado que realiza el remate */
    juzgado: string;
    
    /** Estado de expansión en la interfaz de usuario (solo para visualización) */
    expanded?: boolean;

    /** Estado de expansión del texto original */
    textExpanded?: boolean;
}
