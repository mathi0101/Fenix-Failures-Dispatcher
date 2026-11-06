using Xunit;

// Localización (issue #12): Loc es un singleton global y varios tests mutan su idioma
// activo; xUnit ejecuta las clases en paralelo por defecto y eso genera carreras.
// Se desactiva el paralelismo de colecciones para todo el ensamblado de pruebas.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
