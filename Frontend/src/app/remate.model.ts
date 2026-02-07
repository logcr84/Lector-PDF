export interface RemateFecha {
    label: string;
    precio: number;
    precioDisplay: string;
    fecha: string;
}

export interface Remate {
    id: number;
    tipo: string; // "Vehiculo" | "Propiedad"
    titulo: string;
    area: string;
    precioBase: number;
    precioBaseDisplay: string;
    remates: RemateFecha[];
    expediente: string;
    detalles: { [key: string]: string };
    textoOriginal: string;
    demandado: string;
    juzgado: string;
    
    // UI state
    expanded?: boolean;
}
