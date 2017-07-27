using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;

[TransactionAttribute(TransactionMode.Manual)]
[RegenerationAttribute(RegenerationOption.Manual)]
public class FamilyToDWG : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;

        string keystr = @"Software\FamilyToDWG";
        string revityear = "2015";
        string subkeystr = "TemplateLocation" + revityear;
        var hklm = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        RegistryKey key = hklm.OpenSubKey(keystr, true);

        if (key == null)
        {
            key = Registry.CurrentUser.CreateSubKey(keystr);
            key.SetValue(subkeystr, "");

        }

        if (key.GetValue(subkeystr, null).ToString() == "" || File.Exists(key.GetValue(subkeystr).ToString()) == false)
        {
            var templateFD = new OpenFileDialog();
            templateFD.Filter = "rte files (*.rte)|*.rte";
            templateFD.Title = "Choose a Template";
            templateFD.ShowDialog();
            string docdir = templateFD.FileName;
            key.SetValue(subkeystr, @docdir);
        }

        if (key.GetValue(subkeystr, null).ToString() == null)
        {
            return Result.Failed;
        }

        Autodesk.Revit.DB.Document uiDoc;

        try
        {
            uiDoc = uiApp.Application.NewProjectDocument(key.GetValue(subkeystr).ToString());
        }
        catch
        {
            return Result.Failed;
        }

        if (uiDoc == null)
        {
            return Result.Failed;
        }

        if (!Directory.Exists(@"C:\temp\"))
        {
            Directory.CreateDirectory(@"C:\temp\");
        }

        if (File.Exists(@"C:\temp\new_project.rvt"))
        {
            File.Delete(@"C:\temp\new_project.rvt");
        }

        SaveAsOptions options1 = new SaveAsOptions();
        options1.OverwriteExistingFile = true;
        uiDoc.SaveAs(@"C:/temp/new_project.rvt", options1);

        uiApp.OpenAndActivateDocument(@"C:/temp/new_project.rvt");

        var FD = new OpenFileDialog();
        FD.Filter = "rfa files (*.rfa)|*.rfa";
        FD.Title = "Choose A RevitRFA Family file";
        FD.ShowDialog();

        string filename = FD.SafeFileName;
        string filedir = FD.FileName.Replace(filename, "");

        if (File.Exists(FD.FileName))
        {
            using (Transaction tx = new Transaction(uiDoc))
            {
                tx.Start("Load Family");
                Autodesk.Revit.DB.Family family = null;
                uiDoc.LoadFamily(FD.FileName, out family);
                tx.Commit();

                string name = family.Name;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    FamilySymbol famsymbol = family.Document.GetElement(id) as FamilySymbol;
                    XYZ origin = new XYZ(0, 0, 0);
                    tx.Start("Load Family Member");
                    famsymbol.Activate();
                    FamilyInstance instance = uiDoc.Create.NewFamilyInstance(origin, famsymbol, StructuralType.NonStructural);
                    tx.Commit();

                    DWGExportOptions options = new DWGExportOptions();
                    options.FileVersion = (ACADVersion)(2);
                    options.ExportOfSolids = SolidGeometry.ACIS;
                    options.HideUnreferenceViewTags = true;
                    //options.FileVersion = (ACADVersion)(3);
                    var collector = new FilteredElementCollector(uiDoc);

                    var viewFamilyType = collector
                        .OfClass(typeof(ViewFamilyType))
                        .OfType<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                    // Export the active view
                    ICollection<ElementId> views = new List<ElementId>();
                    tx.Start("ChangeView");
                    View3D view = View3D.CreateIsometric(uiDoc, viewFamilyType.Id);
                    view.DisplayStyle = DisplayStyle.Shading;
                    tx.Commit();
                    views.Add(view.Id);
                    //views.Add(uiDoc.ActiveView.Id);
                    string dwgfilename = famsymbol.Name + ".dwg";
                    string dwgfullfilename = filedir + dwgfilename;
                    if (File.Exists(dwgfullfilename))
                    {
                        File.Delete(dwgfullfilename);
                    }
                    uiDoc.Export(@filedir, @dwgfilename, views, options);

                    tx.Start("Delete Family Member");
                    uiDoc.Delete(instance.Id);
                    tx.Commit();
                }
            }
        }
        else
        {
            Console.WriteLine("Please Create Export Directory For the chosen CAPS file.");
        }

        return Result.Succeeded;
    }
}
