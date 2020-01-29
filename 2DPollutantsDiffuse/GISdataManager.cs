using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.SpatialAnalyst;
using ESRI.ArcGIS.GeoAnalyst;
using DamBreakModelApplication;

namespace _2DPollutantsDiffuse
{
    class GISdataManager
    {
        public static void readRaster(string path, ref IRasterLayer rasterLayer)
        {
            if (path.Trim() == "")
                return;
            string strFileDirectory = path.Substring(0, path.LastIndexOf('\\'));
            string strFileName = path.Substring(path.LastIndexOf('\\') + 1);

            IWorkspaceFactory rasterWorkspaceFac = new RasterWorkspaceFactory();
            IRasterWorkspace rasterWorkspace = rasterWorkspaceFac.OpenFromFile(strFileDirectory, 0) as IRasterWorkspace;
            IRasterDataset rasterDataset = rasterWorkspace.OpenRasterDataset(strFileName);
            IRasterLayer rasterLayer2 = new RasterLayer();
            rasterLayer2.CreateFromDataset(rasterDataset);
            rasterLayer = rasterLayer2;
        }

        public static float[,] Raster2Mat(IRasterLayer rasterlayer)
        {
            IRaster raster = rasterlayer.Raster;
            IRaster2 raster2 = raster as IRaster2;
            float[,] mat = new float[rasterlayer.RowCount, rasterlayer.ColumnCount];
            for (int i = 0; i < rasterlayer.RowCount; i++)
                for (int j = 0; j < rasterlayer.ColumnCount; j++)
                {
                   
                    object value = raster2.GetPixelValue(0, j, i);
                    if (value == null)
                    {
                        mat[i, j] = -9999;
                    }
                    else
                    {
                        mat[i, j] = Convert.ToSingle(raster2.GetPixelValue(0, j, i));
                    }
                }

            return mat;
        }
        public static bool[,] Raster2Mat1(IRasterLayer rasterlayer)
        {
            IRaster raster = rasterlayer.Raster;
            IRaster2 raster2 = raster as IRaster2;
            bool[,] mat = new bool[rasterlayer.RowCount, rasterlayer.ColumnCount];
            for (int i = 0; i < rasterlayer.RowCount; i++)
                for (int j = 0; j < rasterlayer.ColumnCount; j++)
                {

                    object value = raster2.GetPixelValue(0, j, i);
                    if (value == null)
                    {
                        mat[i, j] = false;
                    }
                    else
                    {
                        bool x=Convert.ToBoolean(raster2.GetPixelValue(0, j, i));
                        mat[i, j] = x;
                    }
                }

            return mat;
        }
        public static int[,] Raster2Mat2(IRasterLayer rasterlayer)
        {
            IRaster raster = rasterlayer.Raster;
            IRaster2 raster2 = raster as IRaster2;
            int[,] mat = new int[rasterlayer.RowCount, rasterlayer.ColumnCount];
            for (int i = 0; i < rasterlayer.RowCount; i++)
                for (int j = 0; j < rasterlayer.ColumnCount; j++)
                {

                    object value = raster2.GetPixelValue(0, j, i);
                    if (value == null)
                    {
                        mat[i, j] = 0;
                    }
                    else
                    {
                        mat[i, j] = Convert.ToInt32(raster2.GetPixelValue(0, j, i));
                    }
                }

            return mat;
        }

        public static IRasterDataset exportRasterData(string parth, IRasterLayer rasterLayer, float[,] rasterMat)
        {
            string directory = parth.Substring(0, parth.LastIndexOf("\\"));
            string name = parth.Substring(parth.LastIndexOf("\\") + 1);

            IWorkspaceFactory workspaceFac = new RasterWorkspaceFactoryClass();
            IRasterWorkspace2 rasterWorkspace2 = workspaceFac.OpenFromFile(directory, 0) as IRasterWorkspace2;

            IRasterInfo rasterInfo = (rasterLayer.Raster as IRawBlocks).RasterInfo;
            IPoint originPoint = new Point();
            originPoint.PutCoords(rasterInfo.Origin.X, rasterInfo.Origin.Y - (rasterLayer.Raster as IRasterProps).Height * (rasterLayer.Raster as IRasterProps).MeanCellSize().Y);
            IRasterProps rasterProps = rasterLayer.Raster as IRasterProps;
            IRasterDataset rasterDataSet = rasterWorkspace2.CreateRasterDataset(name, "IMAGINE Image", originPoint, rasterProps.Width, rasterProps.Height, rasterProps.MeanCellSize().X, rasterProps.MeanCellSize().Y, 1, rstPixelType.PT_FLOAT, rasterProps.SpatialReference, true);
            IRaster2 raster2 = rasterDataSet.CreateDefaultRaster() as IRaster2;
            IPnt pntClass = new Pnt();
            pntClass.X = rasterProps.Width;
            pntClass.Y = rasterProps.Height;

            IRasterCursor rasterCursor = raster2.CreateCursorEx(pntClass);
            IRasterEdit rasterEdit = raster2 as IRasterEdit;
            if (rasterEdit.CanEdit())
            {
                IRasterBandCollection bands = rasterDataSet as IRasterBandCollection;
                IPixelBlock3 pixelBlock3 = rasterCursor.PixelBlock as IPixelBlock3;
                System.Array pixels = (System.Array)pixelBlock3.get_PixelData(0);
                for (int i = 0; i < rasterProps.Width; i++)
                {
                    for (int j = 0; j < rasterProps.Height; j++)
                    {
                        pixels.SetValue(Convert.ToSingle(rasterMat[j, i]), i, j);
                    }
                }
                pixelBlock3.set_PixelData(0, (System.Array)pixels);
                rasterEdit.Write(rasterCursor.TopLeft, (IPixelBlock)pixelBlock3);
            }
            (raster2 as IRasterProps).NoDataValue = 0f;
            rasterEdit.Refresh();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(rasterEdit);
            return rasterDataSet;
        }


        public static void readSHP(string path, ref IFeatureLayer featureLayer)
        {
            if (path.Trim() == "")
                return;
            string strFileDirectory = path.Substring(0, path.LastIndexOf('\\'));
            string strFileName = path.Substring(path.LastIndexOf('\\') + 1);


            IWorkspaceFactory pFeatureWsFactory = new ShapefileWorkspaceFactory();
            IWorkspace ws = pFeatureWsFactory.OpenFromFile(strFileDirectory, 0);
            IFeatureWorkspace pFeatureWorkspace = ws as IFeatureWorkspace;
            IFeatureLayer pFeatureLayer = new FeatureLayer();
            //得到图层要素类
            pFeatureLayer.FeatureClass = pFeatureWorkspace.OpenFeatureClass(strFileName);
            pFeatureLayer.Name = pFeatureLayer.FeatureClass.AliasName;
            featureLayer = pFeatureLayer;
        }
    }
}
