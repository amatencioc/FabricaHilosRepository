namespace FabricaHilos.Data
{
    public static class DbInitializer
    {
        public static Task InitializeAsync(ApplicationDbContext context)
        {
            // Sin datos de prueba. La migración se gestiona desde Program.cs.
            return Task.CompletedTask;
        }
    }
}
