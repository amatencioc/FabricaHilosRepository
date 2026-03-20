using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LaColonial.Models;

namespace LaColonial.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    // ── REPOSITORIO ESTÁTICO DE PRODUCTOS ──────────────────────────────────
    private static readonly List<Producto> _productos = new()
    {
        new Producto {
            Slug = "hilados",
            Nombre = "Hilados",
            NombreCorto = "HILADOS",
            ImagenPortada = "galeria/hilados-viscosa.jpg",
            Extracto = "Producimos hilados que van desde 05 a 80 Ne, en uno o más cabos, cardados, peinados, gaseados, pasados en crudo o teñidos, de acuerdo a las necesidades del cliente.",
            ContenidoHtml = @"<p>En La Colonial Fábrica de Hilos S.A. contamos con la línea de hilados, en la cual producimos títulos que van desde 05 a 80 Ne, en uno o más cabos, cardados, peinados, gaseados, pasados en crudo o teñidos, de acuerdo a las necesidades del cliente.</p>
<p>En la línea de hilados producimos:</p>
<ul>
  <li>Hilados de algodón Tangüis cardado y peinado: Rango de título Ne 05/1 al 40/1</li>
  <li>Hilados de algodón Pima peinado: Rango de título Ne 05/1 al 80/1</li>
  <li>Hilados retorcidos: Rango de título Ne 05/4 al 80/2</li>
  <li>Hilados retorcidos, gaseados y gaseados mercenizados</li>
  <li>Hilados crudos y teñidos, de acuerdo a las necesidades del cliente</li>
  <li>Hilados con mezclas: alpaca, tencel, viscosa, lino, modal, acrílico, bambú, entre otras</li>
  <li>Hilados: melange y heather (tanto en Tangüis como en Pima)</li>
</ul>",
            Galeria = new[] {
                "galeria/hilados-viscosa.jpg",
                "galeria/algodon.jpg",
                "galeria/materia-prima-hilos.jpg",
                "galeria/pict.jpg",
                "galeria/galeria_1.jpg",
                "galeria/img_7641_2.jpg",
                "galeria/ingreso_pgs.jpg",
                "galeria/S8C8472.jpg",
            }
        },
        new Producto {
            Slug = "heather",
            Nombre = "Heather",
            NombreCorto = "HEATHER",
            ImagenPortada = "productos/CARTA-HEATHER.jpg",
            Extracto = "Confeccionamos hilados con efecto jaspeado a partir de fibras de colores mezcladas, logrando más de 100 tonalidades exclusivas en nuestra tintorería automatizada.",
            ContenidoHtml = @"<p>El Heather es un hilado especial con efecto jaspeado que se obtiene mezclando fibras de diferentes colores antes del proceso de hilatura. La Colonial produce heather en base algodón Tangüis y Pima, con colores sólidos y mezclas que generan un atractivo efecto visual único en cada prenda.</p>
<p>Características de nuestra línea Heather:</p>
<ul>
  <li>Heather en base algodón Tangüis cardado: efecto rústico y natural</li>
  <li>Heather en base algodón Pima peinado: tacto suave y brillo sutil</li>
  <li>Mezclas con poliéster, viscosa, modal y otras fibras</li>
  <li>Más de 100 tonalidades disponibles en carta de colores propia</li>
  <li>Títulos desde Ne 10/1 hasta Ne 40/1 en presentación cruda o teñida</li>
  <li>Ideal para polos, prendas de punto, tejido artesanal y confecciones casuales</li>
</ul>
<p>La tintorería automatizada de La Colonial garantiza la reproducibilidad del color lote a lote, asegurando uniformidad en toda la producción.</p>",
            Galeria = new[] {
                "productos/CARTA-HEATHER.jpg",
                "productos/chompa.jpg",
                "productos/hilados-viscosa.jpg",
                "galeria/hilados-viscosa.jpg",
                "galeria/algodon.jpg",
                "galeria/img_7641_2.jpg",
            }
        },
        new Producto {
            Slug = "hilos-labores",
            Nombre = "Hilos de Labores",
            NombreCorto = "HILOS DE LABORES",
            ImagenPortada = "galeria/materia-prima-hilos.jpg",
            Extracto = "Hilos diseñados para tejido artesanal, crochet y bordado. Gran variedad de colores, fibras y presentaciones para artesanas y diseñadoras de todo el Perú.",
            ContenidoHtml = @"<p>La línea de Hilos de Labores de La Colonial está especialmente diseñada para el mercado artesanal y de confecciones a mano. Ofrecemos una amplia gama de hilo en diferentes fibras, títulos y colores, listos para tejido a dos agujas, crochet, macramé y bordado.</p>
<p>Tipos de hilos de labores disponibles:</p>
<ul>
  <li><strong>Hilo de tejer</strong> — algodón y mezclas, ideal para tejido a dos agujas y crochet artesanal</li>
  <li><strong>Hilo baby</strong> — ultra suave para prendas infantiles, en colores pastel y neutros</li>
  <li><strong>Hilo de bordado</strong> — acabado brillante, fácil de manejar, alta resistencia al lavado</li>
  <li><strong>Hilo macramé</strong> — mayor grosor para tejido estructurado y decoración</li>
  <li>Disponible en ovillos de 50 g, 100 g y presentaciones industriales</li>
  <li>Más de 80 colores en carta permanente</li>
</ul>
<p>Distribuidores y artesanas pueden solicitar muestras de color o adquirir nuestros hilos de labores directamente en planta o a través de nuestros representantes en Lima y provincias.</p>",
            Galeria = new[] {
                "galeria/materia-prima-hilos.jpg",
                "galeria/galeria_1.jpg",
                "galeria/pict.jpg",
                "galeria/chompa.jpg",
                "galeria/algodon.jpg",
            }
        },
        new Producto {
            Slug = "hilos-costura",
            Nombre = "Hilos de Costura",
            NombreCorto = "HILOS DE COSTURA",
            ImagenPortada = "galeria/colonial-hilos-noticias-calidad.jpg",
            Extracto = "Hilos de costura de alta tenacidad para la industria de confecciones. Spun poliéster, core spun, filamento y blanqueados en múltiples presentaciones y colores.",
            ContenidoHtml = @"<p>Los Hilos de Costura de La Colonial están formulados para brindar la máxima resistencia y uniformidad en las operaciones industriales de costura. Certificados bajo normas internacionales, son la opción preferida de las principales empresas de confecciones del Perú.</p>
<p>Líneas de hilos de costura:</p>
<ul>
  <li><strong>Spun Poliéster</strong> — alta resistencia y suavidad. Para confecciones de ropa en general. Presentaciones: 120 m, 250 m, 500 m, 1000 m, 5000 m</li>
  <li><strong>Core Spun</strong> — núcleo de filamento de poliéster recubierto de algodón o poliéster. Máxima resistencia para jeans, ropa de trabajo y costuras exigentes</li>
  <li><strong>Filamento de Poliéster</strong> — brillo y resistencia al calor para bordado industrial y aplicaciones especiales</li>
  <li><strong>Blanqueados especiales</strong> — para prendas delicadas o de color blanco. Presentación 5,000 m</li>
  <li>Disponible en más de 200 colores con carta de colores actualizada</li>
  <li>Hilos certificados bajo OEKO-TEX Standard 100</li>
</ul>
<p>Para pedidos de hilos de costura industriales, contáctenos en <a href='mailto:ventas@colonial.com.pe'>ventas@colonial.com.pe</a> o al (51-1) 613-0200.</p>",
            Galeria = new[] {
                "galeria/colonial-hilos-noticias-calidad.jpg",
                "galeria/galeria_1.jpg",
                "galeria/algodon.jpg",
                "galeria/materia-prima-hilos.jpg",
            }
        },
    };

    public static Producto? ObtenerProducto(string slug) =>
        _productos.FirstOrDefault(p => p.Slug == slug);

    public static List<Producto> ObtenerProductos() => _productos;

    // ── REPOSITORIO ESTÁTICO DE NOTICIAS ───────────────────────────────────
    private static readonly List<Noticia> _noticias = new()
    {
        new Noticia {
            Slug = "lima-teje-2023", Destacada = true, EsEvento = true,
            Titulo = "La Colonial presente en Lima Teje 2023",
            Categoria = "Ferias & Eventos", Fecha = "Agosto 2023", FechaHtml = "2023-08",
            Imagen = "limateje2023.jpg",
            Extracto = "Participamos en Lima Teje 2023, la feria internacional de tejido y textiles más importante del país, presentando nuestra nueva línea de hilados Nuna y las últimas novedades en heather y fibras naturales.",
            Contenido = @"<p>Lima Teje 2023 fue uno de los eventos más importantes del sector textil artesanal en el Perú. La Colonial Fábrica de Hilos S.A. estuvo presente con un amplio stand donde presentamos toda nuestra gama de hilados premium para tejido artesanal.</p>
<p>Durante la feria, nuestro equipo de ventas atendió a clientes y confeccionistas de todo el país, mostrando de primera mano las características y ventajas de nuestra línea <strong>Nuna</strong>, elaborada con baby alpaca, lana y fibras naturales peruanas de la más alta calidad.</p>
<p>El evento, celebrado en Lima durante agosto de 2023, reunió a cientos de artesanas, diseñadoras y empresas del sector textil. La Colonial consolidó su posición como proveedor de confianza para el mercado artesanal nacional.</p>
<h3>Destacados de nuestra participación</h3>
<ul>
  <li>Presentación exclusiva de la línea Nuna — baby alpaca y lana</li>
  <li>Demostración en vivo de texturas y colores disponibles</li>
  <li>Asesoría técnica personalizada para confeccionistas</li>
  <li>Descuentos especiales para asistentes a la feria</li>
</ul>
<p>Agradecemos a todos quienes nos visitaron y confiaron en La Colonial para sus proyectos textiles. ¡Nos vemos en la próxima edición!</p>"
        },
        new Noticia {
            Slug = "lanzamiento-linea-nuna", Destacada = false, EsEvento = false,
            Titulo = "Lanzamiento línea Nuna — Hilado premium peruano",
            Categoria = "Productos", Fecha = "Agosto 2023", FechaHtml = "2023-08",
            Imagen = "ovillo_Nuna.jpeg",
            Extracto = "Presentamos Nuna, nuestra nueva línea de hilados artesanales elaborados con las mejores fibras peruanas: baby alpaca, lana y mezclas exclusivas para el mercado de tejido artesanal.",
            Contenido = @"<p>Con gran orgullo, La Colonial Fábrica de Hilos S.A. presenta <strong>Nuna</strong>, su nueva línea de hilados artesanales premium elaborada íntegramente con fibras peruanas de primera calidad.</p>
<p>Nuna nace de nuestra apuesta por conectar la riqueza de las fibras naturales del Perú con la exigencia del mercado artesanal moderno. Cada ovillo es el resultado de décadas de experiencia textil y del más riguroso control de calidad.</p>
<h3>Características de la línea Nuna</h3>
<ul>
  <li><strong>Baby Alpaca 100%</strong> — suavidad incomparable, ideal para prendas de lujo</li>
  <li><strong>Merino peruano</strong> — fibra fina, elástica y resistente</li>
  <li><strong>Mezclas exclusivas</strong> — baby alpaca con seda, baby alpaca con lana</li>
  <li>Disponible en más de 40 colores sólidos y tonos naturales</li>
  <li>Presentaciones: ovillos de 50g y 100g</li>
</ul>
<p>La línea Nuna está disponible en nuestra planta de Callao y a través de nuestros distribuidores autorizados en todo el país. Para consultas y pedidos, escríbanos a <a href='mailto:ventas@colonial.com.pe'>ventas@colonial.com.pe</a>.</p>"
        },
        new Noticia {
            Slug = "feria-textil-internacional-2023", Destacada = false, EsEvento = true,
            Titulo = "Presencia en feria textil internacional 2023",
            Categoria = "Internacional", Fecha = "Octubre 2023", FechaHtml = "2023-10",
            Imagen = "IMG-20231026-WA0044.jpg",
            Extracto = "La Colonial participó activamente en ferias textiles internacionales, consolidando su posición como marca referente en la industria textil latinoamericana.",
            Contenido = @"<p>La Colonial Fábrica de Hilos S.A. refuerza su presencia internacional participando en ferias textiles de primer nivel en el extranjero, como parte de su estrategia de expansión hacia mercados de América Latina, Europa y Asia.</p>
<p>Durante octubre de 2023, nuestro equipo comercial representó a La Colonial en el exterior, estableciendo contactos valiosos con importadores, distribuidores y marcas de moda que buscan proveedores de hilados de alta calidad con certificación internacional.</p>
<h3>Resultados de las ferias internacionales 2023</h3>
<ul>
  <li>Nuevos contactos comerciales en 8 países</li>
  <li>Interés confirmado de distribuidores en Europa y Asia</li>
  <li>Presentación de nuestra gama certificada OEKO-TEX y GOTS</li>
  <li>Acuerdos de prueba con 3 marcas internacionales de confecciones</li>
</ul>
<p>Esta apuesta por la internacionalización refleja nuestra visión de convertirnos en el referente latinoamericano de la fabricación de hilados y hilos de alta calidad.</p>"
        },
        new Noticia {
            Slug = "expotextil-peru-2017", Destacada = false, EsEvento = true,
            Titulo = "La Colonial en Expotextil Perú 2017",
            Categoria = "Ferias & Eventos", Fecha = "Setiembre 2017", FechaHtml = "2017-09",
            Imagen = "Expotextil2017.jpg",
            Extracto = "Participamos en Expotextil 2017, la feria más importante de la industria textil peruana, exhibiendo toda nuestra gama de productos y certificaciones internacionales.",
            Contenido = @"<p>La Colonial Fábrica de Hilos S.A. participó en la edición 2017 de Expotextil Perú, la feria más importante de la industria textil y confecciones del país, celebrada en Lima durante setiembre de 2017.</p>
<p>Nuestro stand recibió la visita de centenares de confeccionistas, diseñadores e industriales que pudieron conocer de primera mano la amplitud de nuestra oferta: desde hilados de algodón certificados hasta nuestra nueva gama de heather y mezclas especiales.</p>
<h3>Lo que presentamos en Expotextil 2017</h3>
<ul>
  <li>Nueva gama de hilados heather — algodón con fibras sintéticas</li>
  <li>Colección de hilos de costura industrial en más de 300 colores</li>
  <li>Certificaciones OEKO-TEX Standard 100 y GOTS vigentes</li>
  <li>Programa de muestras gratuitas para confeccionistas</li>
</ul>
<p>Expotextil 2017 reafirmó nuestro compromiso con la industria textil peruana y nos permitió conectar con nuevos clientes nacionales e internacionales.</p>"
        },
        new Noticia {
            Slug = "colombiatex-2021", Destacada = false, EsEvento = true,
            Titulo = "Presentes en Colombiatex 2021",
            Categoria = "Internacional", Fecha = "2021", FechaHtml = "2021",
            Imagen = "not_presentes-en-colombiatex21.jpg",
            Extracto = "La Colonial participó en Colombiatex, una de las ferias textiles más importantes de América Latina, reafirmando su presencia y liderazgo en el mercado internacional.",
            Contenido = @"<p><strong>Colombiatex de las Américas</strong> es una de las más importantes ferias del sector textil en Latinoamérica. En 2021, La Colonial Fábrica de Hilos S.A. participó afirmando su vocación de internacionalización y su capacidad exportadora.</p>
<p>El evento, celebrado en Medellín, Colombia, reunió a empresas textiles de todo el continente. La Colonial presentó su portafolio completo: hilados de algodón, heather, hilos de costura y novedades en fibras naturales certificadas.</p>
<h3>Nuestra participación en Colombiatex 2021</h3>
<ul>
  <li>Exhibición de la gama completa de hilados y heather</li>
  <li>Reuniones con distribuidores de Colombia, Ecuador y Venezuela</li>
  <li>Presentación de certificaciones OEKO-TEX válidas para exportación</li>
  <li>Propuesta comercial para mercado colombiano de confecciones</li>
</ul>
<p>Colombia es uno de los mercados más dinámicos de la confección en América Latina. Nuestra participación en Colombiatex nos permitió fortalecer lazos comerciales y abrir nuevas oportunidades de exportación.</p>"
        },
        new Noticia {
            Slug = "peru-moda-2022", Destacada = false, EsEvento = true,
            Titulo = "La Colonial en Peru Moda 2022",
            Categoria = "Ferias & Eventos", Fecha = "2022", FechaHtml = "2022",
            Imagen = "not_presentes-en-peru-moda22.jpg",
            Extracto = "Participamos en Peru Moda 2022, el evento de moda y textiles más importante del Perú, donde presentamos nuestra colección de hilados, heather y nuevas propuestas para la temporada.",
            Contenido = @"<p>Peru Moda es el evento de moda más importante del Perú y uno de los más relevantes de América Latina, reuniendo a marcas locales e internacionales, compradores, diseñadores y empresas proveedoras del sector textil.</p>
<p>En la edición 2022, La Colonial Fábrica de Hilos S.A. participó con un stand renovado, mostrando toda nuestra colección de hilados artesanales, hilos industriales y la nueva línea de heather, que tuvo una excelente recepción por parte de diseñadores y confeccionistas.</p>
<h3>Destacados de Peru Moda 2022</h3>
<ul>
  <li>Presentación de la nueva paleta de colores — más de 500 tonos disponibles</li>
  <li>Muestra de heather en diferentes composiciones de fibras</li>
  <li>Acuerdos con marcas de moda peruanas para temporada 2022-2023</li>
  <li>Panel sobre sostenibilidad textil con representación de La Colonial</li>
</ul>
<p>Peru Moda 2022 confirma que la industria peruana de la moda apuesta por proveedores nacionales de calidad certificada. La Colonial seguirá siendo el aliado estratégico de las mejores marcas del país.</p>"
        },
        new Noticia {
            Slug = "aniversario-71", Destacada = false, EsEvento = false,
            Titulo = "Celebramos nuestro 71° aniversario",
            Categoria = "Aniversario", Fecha = "Agosto 2016", FechaHtml = "2016-08",
            Imagen = "LA-COLONIAL-ANIVERSARIO-71.jpg",
            Extracto = "La Colonial Fábrica de Hilos celebró 71 años de trayectoria en la industria textil peruana, consolidada como una de las empresas más importantes del sector en Perú.",
            Contenido = @"<p>En agosto de 2016, La Colonial Fábrica de Hilos S.A. celebró con orgullo su <strong>71° aniversario</strong> de fundación, reafirmando su compromiso con la excelencia, la calidad y el desarrollo de la industria textil peruana.</p>
<p>Fundada en 1945, La Colonial ha acompañado el crecimiento del Perú durante más de siete décadas, siendo testigo y protagonista de la evolución de la industria textil nacional. Hoy somos una empresa modernizada, con tecnología de clase mundial y presencia en más de 20 países.</p>
<h3>71 años de logros</h3>
<ul>
  <li>Fundación en 1945 en el Callao, Perú</li>
  <li>Expansión a mercados internacionales desde los años 90</li>
  <li>Certificación OEKO-TEX y GOTS — estándares de excelencia global</li>
  <li>Más de 500 trabajadores directos e indirectos</li>
  <li>Exportaciones a más de 20 países en América, Europa y Asia</li>
</ul>
<p>Gracias a nuestros clientes, trabajadores y aliados comerciales que han hecho posible este camino de 71 años. La Colonial seguirá hilando calidad, tradición e innovación por muchas décadas más.</p>"
        },
        new Noticia {
            Slug = "aniversario-72", Destacada = false, EsEvento = false,
            Titulo = "72 años hilando calidad",
            Categoria = "Aniversario", Fecha = "Agosto 2017", FechaHtml = "2017-08",
            Imagen = "72anoslacolonial.jpg",
            Extracto = "Un año más de dedicación, innovación y excelencia. Celebramos 72 años fabricando hilados y hilos de la más alta calidad para la industria textil nacional e internacional.",
            Contenido = @"<p>En 2017, La Colonial Fábrica de Hilos S.A. cumplió <strong>72 años</strong> de presencia ininterrumpida en la industria textil peruana. Una historia de dedicación, innovación constante y compromiso inquebrantable con la calidad.</p>
<p>Este aniversario fue celebrado con nuestros colaboradores, clientes y socios comerciales, reconociendo que son ellos quienes hacen posible esta trayectoria de excelencia que nos distingue en el mercado nacional e internacional.</p>
<h3>Mirando hacia el futuro</h3>
<ul>
  <li>Inversión en nueva maquinaria de última generación</li>
  <li>Ampliación de la gama de productos con nuevas fibras</li>
  <li>Fortalecimiento del equipo de I+D para innovación textil</li>
  <li>Expansión de exportaciones a nuevos mercados internacionales</li>
</ul>
<p>72 años nos dan la experiencia y la solidez para seguir creciendo. Agradecemos a cada cliente, trabajador y aliado que confía en La Colonial para sus proyectos textiles.</p>"
        },
        new Noticia {
            Slug = "expo-textil-2015", Destacada = false, EsEvento = true,
            Titulo = "La Colonial en Expotextil Perú 2015",
            Categoria = "Ferias & Eventos", Fecha = "Octubre 2015", FechaHtml = "2015-10",
            Imagen = "expotextil2015.png",
            Extracto = "Participación exitosa en Expotextil 2015, donde presentamos nuestra gama de hilados certificados y establecimos nuevas alianzas comerciales con clientes peruanos e internacionales.",
            Contenido = @"<p>La feria Expotextil Perú 2015 se llevó a cabo del 22 al 25 de octubre en el Centro de Exposiciones Jockey de Lima, donde se dieron a conocer las más recientes innovaciones de la industria textil con las propuestas de las diferentes empresas tanto locales como extranjeras, que ofrecen textiles e insumos para la confección, dando la oferta más completa de proveedores para la Industria Textil y Confecciones.</p>
<p>En La Colonial Fábrica de Hilos no pudimos ser ajenos a esta convocatoria y participamos ofreciendo a nuestros clientes las mejores alternativas en hilados de algodón 100% y mezclas con diversas fibras como baby alpaca, modal, tencel, viscosa, entre otras.</p>
<p>El mercado del Perú en el campo de las confecciones es cada vez más importante, y nosotros siempre estamos atentos a las exigencias de nuestros clientes ofreciéndoles los mejores hilados en algodón 100% y mezclas.</p>
<h3>Nuestra participación en Expotextil 2015</h3>
<ul>
  <li>VIII Feria Internacional de Proveedores de la Industria Textil y Confecciones</li>
  <li>Exhibición de hilados de algodón 100% y mezclas especiales</li>
  <li>Presentación de línea de hilos de costura industrial</li>
  <li>Más de 200 contactos comerciales establecidos durante la feria</li>
</ul>
<p>Si desea obtener mayor información puede contactarnos escribiendo a <a href='mailto:ventas@colonial.com.pe'>ventas@colonial.com.pe</a> o llamándonos al (511) 613-0200.</p>"
        },
    };

    public static List<Noticia> ObtenerNoticias() => _noticias;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [Route("empresa")]
    public IActionResult Empresa()
    {
        return View();
    }

    [Route("productos")]
    public IActionResult Productos()
    {
        return View();
    }

    [Route("producto/{slug}")]
    public IActionResult ProductoDetalle(string slug)
    {
        var producto = _productos.FirstOrDefault(p => p.Slug == slug);
        if (producto == null) return NotFound();
        ViewData["Title"] = producto.Nombre;
        return View(producto);
    }

    [Route("galeria")]
    public IActionResult Galeria()
    {
        return View();
    }

    [Route("contacto")]
    [HttpGet]
    public IActionResult Contacto()
    {
        return View();
    }

    [Route("contacto")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contacto(ContactoForm form)
    {
        if (!ModelState.IsValid)
            return View(form);

        TempData["MensajeEnviado"] = "true";
        return RedirectToAction(nameof(Contacto));
    }

    [Route("noticias")]
    public IActionResult Noticias()
    {
        return View(_noticias);
    }

    [Route("noticias/{slug}")]
    public IActionResult NoticiaDetalle(string slug)
    {
        var noticia = _noticias.FirstOrDefault(n => n.Slug == slug);
        if (noticia == null) return NotFound();
        return View(noticia);
    }

    [Route("clientes")]
    public IActionResult Clientes()
    {
        return View();
    }

    [Route("catalogos-electronicos")]
    public IActionResult CatalogosElectronicos()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}


