import 'dart:typed_data';
import 'package:intl/intl.dart';
import 'package:excel/excel.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';
import 'package:file_saver/file_saver.dart';
import '../models/operation.dart';

class ExportService {
  static Future<void> exportToExcel(List<Operation> operations, String category) async {
    var excel = Excel.createExcel();
    Sheet sheetObject = excel['Sheet1'];

    // Header styling
    CellStyle headerStyle = CellStyle(
      bold: true,
      horizontalAlign: HorizontalAlign.Center,
      backgroundColorHex: ExcelColor.blue,
      fontColorHex: ExcelColor.white,
    );

    // Headers
    List<String> headers = ['رقم العملية', 'النوع', 'المبلغ', 'العملة', 'الجهة / الشخص', 'الملاحظات', 'التاريخ', 'الوقت', 'حالة المراجعة'];
    for (int i = 0; i < headers.length; i++) {
      var cell = sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: i, rowIndex: 0));
      cell.value = TextCellValue(headers[i]);
      cell.cellStyle = headerStyle;
    }

    // Data rows
    for (int i = 0; i < operations.length; i++) {
      var op = operations[i];
      int rowIndex = i + 1;
      
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 0, rowIndex: rowIndex)).value = TextCellValue(op.id.toString());
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 1, rowIndex: rowIndex)).value = TextCellValue(op.category ?? '');
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 2, rowIndex: rowIndex)).value = TextCellValue(op.amount?.toString() ?? '0');
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 3, rowIndex: rowIndex)).value = TextCellValue(op.currency ?? '');
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 4, rowIndex: rowIndex)).value = TextCellValue(op.party ?? '');
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 5, rowIndex: rowIndex)).value = TextCellValue(op.notes ?? '');
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 6, rowIndex: rowIndex)).value = TextCellValue(DateFormat('yyyy-MM-dd').format(op.createdAt));
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 7, rowIndex: rowIndex)).value = TextCellValue(DateFormat('hh:mm a').format(op.createdAt));
      sheetObject.cell(CellIndex.indexByColumnRow(columnIndex: 8, rowIndex: rowIndex)).value = TextCellValue(op.isReviewed ? 'مراجعة' : 'غير مراجعة');
    }

    // Adjust column widths (approximate)
    sheetObject.setColumnWidth(0, 15);
    sheetObject.setColumnWidth(1, 15);
    sheetObject.setColumnWidth(2, 15);
    sheetObject.setColumnWidth(3, 10);
    sheetObject.setColumnWidth(4, 25);
    sheetObject.setColumnWidth(5, 30);
    sheetObject.setColumnWidth(6, 15);
    sheetObject.setColumnWidth(7, 15);
    sheetObject.setColumnWidth(8, 15);

    List<int>? fileBytes = excel.encode();
    if (fileBytes != null) {
      final dateStr = DateFormat('yyyy_MM_dd_HH_mm').format(DateTime.now());
      final fileName = 'تقرير_عمليات_$category\_$dateStr.xlsx';
      await FileSaver.instance.saveFile(
        name: fileName,
        bytes: Uint8List.fromList(fileBytes),
        mimeType: MimeType.microsoftExcel,
      );
    }
  }

  static Future<void> exportToPdf(List<Operation> operations, String category) async {
    final pdf = pw.Document();
    
    // Load Arabic Font
    final arabicFont = await PdfGoogleFonts.cairoRegular();
    final arabicFontBold = await PdfGoogleFonts.cairoBold();

    pdf.addPage(
      pw.MultiPage(
        pageFormat: PdfPageFormat.a4.landscape,
        textDirection: pw.TextDirection.rtl,
        theme: pw.ThemeData.withFont(
          base: arabicFont,
          bold: arabicFontBold,
        ),
        header: (context) {
          return pw.Container(
            alignment: pw.Alignment.center,
            margin: const pw.EdgeInsets.only(bottom: 20),
            child: pw.Text('تقرير عمليات $category', style: pw.TextStyle(fontSize: 24, fontWeight: pw.FontWeight.bold)),
          );
        },
        build: (context) {
          return [
            pw.TableHelper.fromTextArray(
              context: context,
              cellAlignment: pw.Alignment.centerRight,
              headerDecoration: const pw.BoxDecoration(color: PdfColors.blueGrey300),
              headerStyle: pw.TextStyle(color: PdfColors.white, fontWeight: pw.FontWeight.bold),
              cellStyle: const pw.TextStyle(fontSize: 10),
              border: pw.TableBorder.all(color: PdfColors.grey300),
              headers: ['حالة المراجعة', 'الوقت', 'التاريخ', 'الملاحظات', 'الجهة', 'العملة', 'المبلغ', 'النوع', 'رقم العملية'].reversed.toList(),
              data: operations.map((op) {
                return [
                  op.isReviewed ? 'مراجعة' : 'غير مراجعة',
                  DateFormat('hh:mm a').format(op.createdAt),
                  DateFormat('yyyy-MM-dd').format(op.createdAt),
                  op.notes ?? '',
                  op.party ?? '',
                  op.currency ?? '',
                  op.amount?.toString() ?? '0',
                  op.category ?? '',
                  op.id.toString(),
                ].reversed.toList();
              }).toList(),
            ),
          ];
        },
      ),
    );

    final dateStr = DateFormat('yyyy_MM_dd_HH_mm').format(DateTime.now());
    final fileName = 'تقرير_عمليات_$category\_$dateStr.pdf';
    final Uint8List pdfBytes = await pdf.save();

    await FileSaver.instance.saveFile(
      name: fileName,
      bytes: pdfBytes,
      mimeType: MimeType.pdf,
    );
  }
}
