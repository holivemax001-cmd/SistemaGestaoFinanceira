using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaGestaoFinanceira.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteIdEntidadesFinanceiras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Receitas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Despesas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "ContasFinanceiras",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "CategoriasFinanceiras",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receitas_ClienteId",
                table: "Receitas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_ClienteId",
                table: "Despesas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasFinanceiras_ClienteId",
                table: "ContasFinanceiras",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinanceiras_ClienteId",
                table: "CategoriasFinanceiras",
                column: "ClienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_CategoriasFinanceiras_Clientes_ClienteId",
                table: "CategoriasFinanceiras",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasFinanceiras_Clientes_ClienteId",
                table: "ContasFinanceiras",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Despesas_Clientes_ClienteId",
                table: "Despesas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receitas_Clientes_ClienteId",
                table: "Receitas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategoriasFinanceiras_Clientes_ClienteId",
                table: "CategoriasFinanceiras");

            migrationBuilder.DropForeignKey(
                name: "FK_ContasFinanceiras_Clientes_ClienteId",
                table: "ContasFinanceiras");

            migrationBuilder.DropForeignKey(
                name: "FK_Despesas_Clientes_ClienteId",
                table: "Despesas");

            migrationBuilder.DropForeignKey(
                name: "FK_Receitas_Clientes_ClienteId",
                table: "Receitas");

            migrationBuilder.DropIndex(
                name: "IX_Receitas_ClienteId",
                table: "Receitas");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_ClienteId",
                table: "Despesas");

            migrationBuilder.DropIndex(
                name: "IX_ContasFinanceiras_ClienteId",
                table: "ContasFinanceiras");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasFinanceiras_ClienteId",
                table: "CategoriasFinanceiras");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Receitas");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "ContasFinanceiras");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "CategoriasFinanceiras");
        }
    }
}
