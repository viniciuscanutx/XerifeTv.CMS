using MongoDB.Driver.Linq;
using OfficeOpenXml;
using XerifeTv.CMS.Modules.Abstractions.Exceptions;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

public class SpreadsheetReaderService : ISpreadsheetReaderService
{
    public string[][] Read(string[] colluns, MemoryStream fileStream, int worksheetIndex = 0)
    {
        try
        {
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[worksheetIndex] 
                ?? throw new SpreadsheetInvalidException("Planilha vazia ou nao encontrada");

            ICollection<string> rowItemValues = [];
            ICollection<string[]> result = [];

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var firstCell = worksheet.Cells[row, 1].Text?.Trim();
                if (string.IsNullOrEmpty(firstCell)) continue;

                for (int col = 1; col <= colluns.Length; col++)
                    rowItemValues.Add(worksheet.Cells[row, col].Text?.Trim() ?? string.Empty);

                result.Add([.. rowItemValues]);
                rowItemValues.Clear();
            }

			return [.. result];
        }
        catch (Exception)
        {
            throw;
        }
    }
}