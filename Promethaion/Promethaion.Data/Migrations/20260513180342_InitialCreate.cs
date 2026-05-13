using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Promethaion.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrawResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrawDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DrawNumber = table.Column<int>(type: "int", nullable: false),
                    Ball1 = table.Column<int>(type: "int", nullable: false),
                    Ball2 = table.Column<int>(type: "int", nullable: false),
                    Ball3 = table.Column<int>(type: "int", nullable: false),
                    Ball4 = table.Column<int>(type: "int", nullable: false),
                    Ball5 = table.Column<int>(type: "int", nullable: false),
                    Ball6 = table.Column<int>(type: "int", nullable: false),
                    BonusBall = table.Column<int>(type: "int", nullable: true),
                    GameName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PipelineName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TrainingSetSize = table.Column<int>(type: "int", nullable: false),
                    TestSetSize = table.Column<int>(type: "int", nullable: false),
                    MeanAbsoluteError = table.Column<double>(type: "float", nullable: false),
                    RootMeanSquaredError = table.Column<double>(type: "float", nullable: false),
                    RSquared = table.Column<double>(type: "float", nullable: false),
                    MacroAccuracy = table.Column<double>(type: "float", nullable: false),
                    MicroAccuracy = table.Column<double>(type: "float", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    AverageMatchCount = table.Column<double>(type: "float", nullable: false),
                    IsBestVersion = table.Column<bool>(type: "bit", nullable: false),
                    DiagnosticsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetDrawNumber = table.Column<int>(type: "int", nullable: false),
                    Ball1 = table.Column<int>(type: "int", nullable: false),
                    Ball2 = table.Column<int>(type: "int", nullable: false),
                    Ball3 = table.Column<int>(type: "int", nullable: false),
                    Ball4 = table.Column<int>(type: "int", nullable: false),
                    Ball5 = table.Column<int>(type: "int", nullable: false),
                    Ball6 = table.Column<int>(type: "int", nullable: false),
                    BonusBall = table.Column<int>(type: "int", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    PerBallConfidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActualDrawResultId = table.Column<int>(type: "int", nullable: true),
                    MatchCount = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_DrawResults_ActualDrawResultId",
                        column: x => x.ActualDrawResultId,
                        principalTable: "DrawResults",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrawResults_DrawDate",
                table: "DrawResults",
                column: "DrawDate");

            migrationBuilder.CreateIndex(
                name: "IX_DrawResults_DrawNumber_GameName",
                table: "DrawResults",
                columns: new[] { "DrawNumber", "GameName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelMetrics_PipelineName_IsBestVersion",
                table: "ModelMetrics",
                columns: new[] { "PipelineName", "IsBestVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ActualDrawResultId",
                table: "Predictions",
                column: "ActualDrawResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_TargetDrawNumber",
                table: "Predictions",
                column: "TargetDrawNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelMetrics");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "DrawResults");
        }
    }
}
