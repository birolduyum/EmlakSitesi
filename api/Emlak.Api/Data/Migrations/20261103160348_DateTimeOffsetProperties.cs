using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Emlak.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DateTimeOffsetProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Yalnızca CLR türü değişti (DateTime -> DateTimeOffset); sütunlar zaten "timestamp with time zone".
            // Hiçbir veriye dokunulmaz: EF'in HasData tohum satırları için ürettiği UpdateData çağrıları bilerek çıkarıldı
            // (kuru yöneticinin değiştirdiği bir veritabanında UpdatedAt'i tohum tarihine geri yazardı).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tür değişikliği geri alınırken de veri değişmez.
        }
    }
}
