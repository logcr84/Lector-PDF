using Xunit;
using Backend.Services;
using Backend.Models;
using System.Collections.Generic;
using System.Linq;

namespace Backend.Tests
{
    public class SpecificEdictsTests
    {
        private readonly PdfParserService _parser;

        public SpecificEdictsTests()
        {
            _parser = new PdfParserService();
        }

        [Fact]
        public void Parse_Edicto1_Propiedad_ShouldExtractCorrectData()
        {
            // Arrange
            string text = @"En este Despacho, con una base de CUARENTA Y DOS MILLONES DOSCIENTOS QUINCE MIL CINCUENTA Y NUEVE COLONES
CON CUARENTA Y OCHO CÉNTIMOS (¢42.215.059,48), soportando hipoteca de primer grado citas: 2010-246052-01-0005-001 y
RESERVAS DE LEY DE AGUAS Y LEY DE CAMINOS PÚBLICOS CITAS: 416-04829-01-0297-001, sáquese a remate la finca del
partido de Alajuela, matrícula número 469228, derecho 000, para lo cual se señalan las NUEVE HORAS DEL DOCE DE FEBRERO
DEL DOS MIL VEINTISÉIS (9:00 AM DEL 12-02-2026). De no haber postores, el segundo remate se efectuará a las NUEVE HORAS
DEL VEINTE DE FEBRERO DEL DOS MIL VEINTISÉIS (9:00 AM DEL 20-02-2026) la finca con la base de TREINTA Y UN MILLONES
SEISCIENTOS SESENTA Y UN MIL DOSCIENTOS NOVENTA Y CUATRO COLONES CON SESENTA Y UN CÉNTIMOS
(¢31.661.294,61) (75% de la base original) y de continuar sin oferentes, para el tercer remate se señalan las NUEVE HORAS DEL
DOS DE MARZO DEL DOS MIL VEINTISÉIS (9:00 AM DEL 02-03-2026) La finca con la base de DIEZ MILLONES QUINIENTOS
CINCUENTA Y TRES MIL SETECIENTOS SESENTA Y CUATRO COLONES CON OCHENTA Y SIETE CÉNTIMOS (¢10.553.764,87)
(25% de la base original). NOTAS: Se le informa a las personas interesadas en participar en la almoneda que en caso de pagar con
cheque certificado, el mismo deberá ser emitido a favor de este despacho. Publíquese este edicto por dos veces consecutivas, la
primera publicación con un mínimo de cinco días de antelación a la fecha fijada para la subasta. Se remata por ordenarse así en
PROCESO OTROS EXTREMOS PATRIMONIALES Y NO PATRIMONIALES. de MANFRED MIGUEL PICADO MORA contra DEILY
ALVARADO SEGURA, LAS DELICIAS DEL TENORIO SOCIEDAD CIVIL EXP:21-000124-1516-LA JUZGADO CIVIL, TRABAJO Y
AGRARIO DEL II CIRCUITO JUDICIAL DE ALAJUELA, SEDE UPALA (LABORAL). Fecha 03 de febrero del año 2026. Msc. MÓNICA
MORA VÍLCHEZ JUEZA LABORAL.
Referencia N°: 2026184561, publicación número: 1 de 2";

            // Act
            var remates = _parser.ParseText(text);

            // Assert
            Assert.Single(remates);
            var remate = remates[0];

            Assert.Equal("Propiedad", remate.Tipo);
            Assert.Equal("21-000124-1516-LA", remate.Expediente);
            Assert.Equal(42215059.48m, remate.PrecioBase);
            Assert.Equal("469228", remate.Detalles["Matricula"]);

            Assert.Equal(3, remate.Remates.Count);

            // 1st Auction
            Assert.Equal("12/02/2026 09:00", remate.Remates[0].Fecha);
            Assert.Equal(42215059.48m, remate.Remates[0].Precio);

            // 2nd Auction
            Assert.Equal("20/02/2026 09:00", remate.Remates[1].Fecha);
            // Verify approximate value or exact depending on logic
            Assert.Equal(31661294.61m, remate.Remates[1].Precio);

            // 3rd Auction
            Assert.Equal("02/03/2026 09:00", remate.Remates[2].Fecha);
            Assert.Equal(10553764.87m, remate.Remates[2].Precio);
        }

        [Fact]
        public void Parse_Edicto2_Vehiculo_ShouldExtractCorrectData()
        {
            // Arrange
            string text = @"En este Despacho, con una base de VEINTISÉIS MIL OCHOCIENTOS SESENTA Y TRES DÓLARES EXACTOS, libre de gravámenes prendarios, pero soportando DENUNCIA DE TRANSITO CITAS 0800-01284308-001, DENUNCIA DE TRANSITO CITAS 0800- 01411937-001; sáquese a remate el vehículo PLACA: CBY660, Marca: KAIYI, Estilo: X3 PRO LUX, Categoría: AUTOMOVIL, Capacidad: 5 personas # de Serie: LUYJB2G25SA000886, Carrocería: TODO TERRENO 4 PUERTAS, Tracción: 4X2, Año Fabricación: 2025, Color: NEGRO, N. Motor: SQRE4T15CBEPL60243. Para tal efecto se señalan las ocho horas cuarenta y cinco minutos del catorce de abril de dos mil veintiséis. De no haber postores, el segundo remate se efectuará a las ocho horas cuarenta y cinco minutos del veintidós de abril de dos mil veintiséis con la base de VEINTE MIL CIENTO CUARENTA Y SIETE DÓLARES CON VEINTICINCO CENTAVOS (75% de la base original) y de continuar sin oferentes, para el tercer remate se señalan las ocho horas cuarenta y cinco minutos del treinta de abril de dos mil veintiséis con la base de SEIS MIL SETECIENTOS QUINCE DÓLARES CON SETENTA Y CINCO CENTAVOS (25% de la base original). NOTAS: Se le informa a las personas interesadas en participar en laalmoneda que en caso de pagar con cheque certificado, el mismo deberá ser emitido a favor de este despacho. Publíquese este edicto por dos veces consecutivas, la primera publicación con un mínimo de cinco días de antelación a la fecha fijada para la subasta.- Se remata por ordenarse así en PROCESO EJECUCIÓN PRENDARIA de COOPERATIVA DE AHORRO Y CREDITO ANDE NUMERO UNO R contra ARIEL CABRERA TALENO EXP:25-011311-1338-CJ JUZGADO TERCERO ESPECIALIZADO DE COBRO DEL I CIRCUITO JUDICIAL DE SAN JOSÉ. 06 de noviembre del año 2025. Lic. Verny Gustavo Arias Vega, Juez/a Tramitador/a. Referencia N°: 2026184545, publicación número: 1 de 2";

            // Act
            var remates = _parser.ParseText(text);

            // Assert
            Assert.Single(remates);
            var remate = remates[0];

            Assert.Equal("Vehiculo", remate.Tipo);
            Assert.Equal("25-011311-1338-CJ", remate.Expediente);
            Assert.Equal("CBY660", remate.Detalles["Placa"]);
            Assert.Equal("KAIYI", remate.Detalles["Marca"]);

            // Expected price: 26863
            Assert.Equal(26863m, remate.PrecioBase);

            Assert.Equal(3, remate.Remates.Count);

            // 1st
            Assert.Equal("14/04/2026 08:45", remate.Remates[0].Fecha);

            // 2nd
            Assert.Equal("22/04/2026 08:45", remate.Remates[1].Fecha);

            // 3rd
            Assert.Equal("30/04/2026 08:45", remate.Remates[2].Fecha);
        }

        [Fact]
        public void Parse_Edicto3_Propiedad_SanJose_ShouldExtractCorrectData()
        {
            // Arrange
            string text = @"En este Despacho, Con una base de TRECE MILLONES SESENTA MIL COLONES EXACTOS, libre de gravámenes hipotecarios, pero soportando SERVIDUMBRE TRASLADADA CITAS: 332-00762-01-0002-001; sáquese a remate la finca del partido de SAN JOSÉ, matrícula número quinientos dos mil ochenta y cuatro, derecho 000, la cual es terreno BLOQUE F LOTE 16 TERRENO PARA CONSTRUIR. Situada en el DISTRITO SAN FELIPE, CANTÓN ALAJUELITA, de la provincia de SAN JOSÉ. COLINDA: al NORTE LOTE 18 F Y LOTE 17 F; al SUR CALLE 4 CON UN FRENTE DE 16 METROS CON SESENTA CÉNTÍMETROS; al ESTE ÁREA DE JUEGOS INFANTILES y al OESTE LOTE 15 F. MIDE: CIENTO VEINTIÚN METROS CON SESENTA Y SEIS DECÍMETROS CUADRADOS METROS CUADRADOS. Para tal efecto, se señalan las catorce horas cero minutos del veintiséis de mayo de dos mil veintiséis. De no haber postores, el segundo remate se efectuará a las catorce horas cero minutos del tres de junio de dos mil veintiséis con la base de NUEVE MILLONES SETECIENTOS NOVENTA Y CINCO MIL COLONES EXACTOS (75% de la base original) y de continuar sin oferentes, para el tercer remate se señalan las catorce horas cero minutos del once de junio de dos mil veintiséis con la base de TRES MILLONES DOSCIENTOS SESENTA Y CINCO MIL COLONES EXACTOS (25% de la base original). NOTAS: Se le informa a las personas interesadas en participar en la almoneda que en caso de pagar con cheque certificado, el mismo deberá ser emitido a favor de este despacho. Publíquese este edicto por dos veces consecutivas, la primera publicación con un mínimo de cinco días de antelación a la fecha fijada para la subasta. Se remata por ordenarse así en PROCESO EJECUCIÓN HIPOTECARIA de ANNY GRISEL ALCANTARA MANCEBO contra CARLOS ROBERTO GONZALEZ ANDRADE EXP:25-009923-1338- CJ JUZGADO TERCERO ESPECIALIZADO DE COBRO DEL I CIRCUITO JUDICIAL DE SAN JOSÉ. 13 de enero del año 2026. Yanin Argerie Torrentes Avila, Juez/a Tramitador/a. Referencia N°: 2026184543, publicación número: 1 de 2";

            // Act
            var remates = _parser.ParseText(text);

            // Assert
            Assert.Single(remates);
            var remate = remates[0];

            Assert.Equal("Propiedad", remate.Tipo);
            Assert.Equal("25-009923-1338-CJ", remate.Expediente.Replace(" ", "")); // Normalizing spaces in expected

            // Price: 13,060,000
            Assert.Equal(13060000m, remate.PrecioBase);

            // Dates
            Assert.Equal("26/05/2026 14:00", remate.Remates[0].Fecha);
            Assert.Equal("03/06/2026 14:00", remate.Remates[1].Fecha);
            Assert.Equal("11/06/2026 14:00", remate.Remates[2].Fecha);
        }
    }
}
