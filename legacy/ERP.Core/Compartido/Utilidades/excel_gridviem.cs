using System;
using System.Data;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Utilidades
{
    public class excel_gridviem
    {
        public void CargaDatosAExcel_gridviem(DataGridView DgvDatos, string nombre_hoja)
        {
            if ((DgvDatos.Columns.Count == 0) || (DgvDatos.Rows.Count == 0))
            {
                return;
            }

            // Creating dataset to export
            DataSet dset = new DataSet();
            // add table to dataset
            dset.Tables.Add();
            // add column to that table
            for (int i = 0; i < DgvDatos.ColumnCount; i++)
            {
                dset.Tables[0].Columns.Add(DgvDatos.Columns[i].HeaderText);
            }
            // add rows to the table
            DataRow dr1;
            for (int i = 0; i < DgvDatos.RowCount; i++)
            {
                dr1 = dset.Tables[0].NewRow();
                for (int j = 0; j < DgvDatos.Columns.Count; j++)
                {
                    dr1[j] = DgvDatos.Rows[i].Cells[j].Value;
                }
                dset.Tables[0].Rows.Add(dr1);
            }

            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            object m_Excel = Activator.CreateInstance(excelType);
            object workbooks = excelType.InvokeMember("Workbooks", System.Reflection.BindingFlags.GetProperty, null, m_Excel, null);
            object objLibroExcel = workbooks.GetType().InvokeMember("Add", System.Reflection.BindingFlags.InvokeMethod, null, workbooks, null);
            object worksheets = objLibroExcel.GetType().InvokeMember("Worksheets", System.Reflection.BindingFlags.GetProperty, null, objLibroExcel, null);
            object objHojaExcel = worksheets.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, worksheets, new object[] { 1 });
            objHojaExcel.GetType().InvokeMember("Name", System.Reflection.BindingFlags.SetProperty, null, objHojaExcel, new object[] { nombre_hoja });
            objHojaExcel.GetType().InvokeMember("Activate", System.Reflection.BindingFlags.InvokeMethod, null, objHojaExcel, null);

            DataTable dt = dset.Tables[0];
            int colIndex = 0;
            int rowIndex = 0;

            object cells = excelType.InvokeMember("Cells", System.Reflection.BindingFlags.GetProperty, null, m_Excel, null);

            foreach (DataColumn dc in dt.Columns)
            {
                colIndex = colIndex + 1;
                object cell = cells.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, cells, new object[] { 1, colIndex });
                cell.GetType().InvokeMember("Value2", System.Reflection.BindingFlags.SetProperty, null, cell, new object[] { dc.ColumnName });
            }

            foreach (DataRow dr in dt.Rows)
            {
                rowIndex = rowIndex + 1;
                colIndex = 0;
                foreach (DataColumn dc in dt.Columns)
                {
                    colIndex = colIndex + 1;
                    object cell = cells.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, cells, new object[] { rowIndex + 1, colIndex });
                    cell.GetType().InvokeMember("Value2", System.Reflection.BindingFlags.SetProperty, null, cell, new object[] { dr[dc.ColumnName] });
                }
            }

            // Bold header row
            object sheetCells = objHojaExcel.GetType().InvokeMember("Cells", System.Reflection.BindingFlags.GetProperty, null, objHojaExcel, null);
            object cell1 = sheetCells.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, sheetCells, new object[] { 1, 1 });
            object cell2 = sheetCells.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, sheetCells, new object[] { 1, DgvDatos.Columns.Count });
            object range = objHojaExcel.GetType().InvokeMember("Range", System.Reflection.BindingFlags.GetProperty, null, objHojaExcel, new object[] { cell1, cell2 });
            object font = range.GetType().InvokeMember("Font", System.Reflection.BindingFlags.GetProperty, null, range, null);
            font.GetType().InvokeMember("Bold", System.Reflection.BindingFlags.SetProperty, null, font, new object[] { true });

            // AutoFit columns
            object columns = objHojaExcel.GetType().InvokeMember("Columns", System.Reflection.BindingFlags.GetProperty, null, objHojaExcel, null);
            columns.GetType().InvokeMember("AutoFit", System.Reflection.BindingFlags.InvokeMethod, null, columns, null);

            string strFileName = System.IO.Path.GetTempFileName();
            bool blnFileOpen = false;
            try
            {
                System.IO.FileStream fileTemp = System.IO.File.OpenWrite(strFileName);
                fileTemp.Close();
            }
            catch (Exception)
            {
                blnFileOpen = false;
            }

            if (System.IO.File.Exists(strFileName))
            {
                System.IO.File.Delete(strFileName);
            }

            objLibroExcel.GetType().InvokeMember("SaveAs", System.Reflection.BindingFlags.InvokeMethod, null, objLibroExcel, new object[] { strFileName });
            workbooks.GetType().InvokeMember("Open", System.Reflection.BindingFlags.InvokeMethod, null, workbooks, new object[] { strFileName });
            excelType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, m_Excel, new object[] { true });
        }
    }
}
