using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesRaster;
using System.Threading;

namespace _2DPollutantsDiffuse
{
    public partial class Calculate2DCA : Form
    {
        /// <summary>
        /// 模型所有参数
        /// </summary>
        #region
        //污染物输入模式是否为自定义模式
        bool CustomMode = false;

        //INPUT PARA
        float Mtotal; //模式1：Total Mass污染总浓度 [kg]
        IFeatureLayer PollutantFeatureLayer = new FeatureLayer();//模式1：污染物中心位置点图层
	    IPoint PollutantPoint;//模式1：污染物中心位置 
        int rOfPollutant; //模式1：污染物半径
        IRasterLayer PollutantDistribution = new RasterLayer();//模式2：污染物分布
	    float[,] LF; //陆地or水面
        IRasterLayer LFrasterlayer = new RasterLayer();
	    float m,d; //垂直方向扩散系数m，斜向扩散系数d
	    int[,] CD; //CurrentDirection 流向(8)
        IRasterLayer CDrasterlayer = new RasterLayer();
	    float[,] CV; //CurrentVelocity [m/sec]
        IRasterLayer CVrasterlayer = new RasterLayer();
	    int[,] WD; //WindDirection(8)
        IRasterLayer WDrasterlayer = new RasterLayer();
	    float[,] WV; //WindVelocity [m/sec]
        IRasterLayer WVrasterlayer = new RasterLayer();
	    float tem;//temperature [K]
	    float p; //蒸散系数 [kg(sec K)-1]
	    float totalTime; //模拟总时长 [sec]
	    float otimeStep;//输出步长 [sec]
	    float threshold; //浓度阈值

        //PATH PARA
        string PollutantInputPath, LandOrRiverPath, WindDPath, WindVPath, CurrentVPath, CurrentDPath, PollutionPostionPath, SavePollutionDataPath;
       
	    //MID PARA
        int rowCount, columnCount;
        float cellLength, cellLength2;
	    int timeStep;
	    float[,] initialM;//初始浓度矩阵
        List<int[]> temMIndex; //存储有污染物的cells，索引
	    bool[,] arrived;//记录是否有污染
        //float averageCV;//平局流速
        float maxWindV = 0, maxCurrentV = 0;//最大风速流速计算
        float r = 0;//风的影响参数

        //RESULT PARA
        float[,] M;//初始浓度矩阵
        float[,] outM;

        //线程与进度条窗口
        DamBreakModelApplication.ProcessManager pm;
        Thread thread;

        #endregion

        public Calculate2DCA()
        {
            InitializeComponent();
            //Mode1
            txbMtotal.Text = "1000";//kg
            txbR.Text = "1";//个像素范围
            txbPollutionPostion.Text = @"C:\Users\Glenn Luo\Desktop\test\2Ddiffuse\center.shp";//位置shp
            //Mode2
            txbPollutantInput.Text = @"C:\Users\Glenn Luo\Desktop\test\2Ddiffuse\distri1.img";
            txbTemperature.Text = "0";
            txbLandOrRiver.Text = @"D:\MYGIS Project\二维水污染扩散实验数据\Test2\FL\FL30.img"; 
            txbWindD.Text = @"D:\MYGIS Project\二维水污染扩散实验数据\Test2\WD&V\WD.img";
            txbWindV.Text = @"D:\MYGIS Project\二维水污染扩散实验数据\Test2\WD&V\WV.img";
            txbCurrentD.Text = @"D:\MYGIS Project\二维水污染扩散实验数据\Test2\CD&V\CD.img";
            txbCurrentV.Text = @"D:\MYGIS Project\二维水污染扩散实验数据\Test2\CD&V\CV.img";
            txbm.Text = "0.084"; 
            txbd.Text = "1";     
            txbp.Text = "0";
            txbThreshold.Text = "0.001";
            txbTotalTime.Text = "7200";
            txbOutTime.Text = "600";
            txbSavePollutionData.Text = @"C:\Users\Glenn Luo\Desktop\test\2Ddiffuse\out99\d";
        }
        //
        void CaculatePolDiffuse()
        {
            initalData();// 初始化一些栅格数据到矩阵
            initialCaculate();//初始化污染计算初始状态
            startCaculate();//开始计算
            if (thread != null)
            {
                thread.Abort();
                this.Invoke(pm.close);
            }
            this.Close();
        } 
        //
        void initalData()
        {
            pm = new DamBreakModelApplication.ProcessManager();
            this.Invoke(pm.createProcessWindow());
            PollutantInputPath = txbPollutantInput.Text;
            LandOrRiverPath = txbLandOrRiver.Text;
            WindDPath = txbWindD.Text;
            WindVPath = txbWindV.Text;
            CurrentDPath = txbCurrentD.Text;
            CurrentVPath = txbCurrentV.Text;
            PollutionPostionPath = txbPollutionPostion.Text;
            SavePollutionDataPath = txbSavePollutionData.Text;

            if ( CustomMode == false )
            {
                Mtotal = float.Parse( txbMtotal.Text.Trim());
                rOfPollutant = int.Parse(txbR.Text.Trim());
                GISdataManager.readSHP(PollutionPostionPath, ref PollutantFeatureLayer);
                IFeatureClass featureClass = PollutantFeatureLayer.FeatureClass;
                IFeature feature = featureClass.GetFeature(0);
                IGeometry Geo = feature.Shape;
                PollutantPoint = Geo as IPoint;
            }
            else
            {
                GISdataManager.readRaster(PollutantInputPath, ref PollutantDistribution); 
            }
            temMIndex = new List<int[]>();
            tem = float.Parse(txbTemperature.Text.Trim());
            GISdataManager.readRaster(LandOrRiverPath, ref LFrasterlayer);
            this.Invoke(pm.updateProcess, new object[] { string.Format("正在读取LF数据："), 0 });
            LF = GISdataManager.Raster2Mat(LFrasterlayer);
            GISdataManager.readRaster(WindDPath, ref WDrasterlayer);
            //this.Invoke(pm.updateProcess, new object[] { string.Format("正在读取WD数据："), 0 });
            //WD = GISdataManager.Raster2Mat2(WDrasterlayer);
            WD = new int[WDrasterlayer.RowCount,WDrasterlayer.ColumnCount];
            WV = new float[WDrasterlayer.RowCount, WDrasterlayer.ColumnCount];
            CD = new int[WDrasterlayer.RowCount, WDrasterlayer.ColumnCount];
            CV = new float[WDrasterlayer.RowCount, WDrasterlayer.ColumnCount]; 
            for (int i = 0; i < WDrasterlayer.RowCount; i++)
            {
                for (int j = 0; j < WDrasterlayer.ColumnCount; j++)
                {
                    WD[i, j] = 0;
                    WV[i, j] = 0;
                }
            }
            //GISdataManager.readRaster(WindVPath, ref WVrasterlayer);
            //this.Invoke(pm.updateProcess, new object[] { string.Format("正在读取WV数据："), 0 });
            //WV = GISdataManager.Raster2Mat(WVrasterlayer);
            GISdataManager.readRaster(CurrentDPath, ref CDrasterlayer);
            this.Invoke(pm.updateProcess, new object[] { string.Format("正在读取CD数据："), 0 });
            CD = GISdataManager.Raster2Mat2(CDrasterlayer);
            GISdataManager.readRaster(CurrentVPath, ref CVrasterlayer);
            this.Invoke(pm.updateProcess, new object[] { string.Format("正在读取CV数据："), 0 });
            CV = GISdataManager.Raster2Mat(CVrasterlayer);
            m = float.Parse(txbm.Text.Trim());
            d = float.Parse(txbd.Text.Trim());
            p = float.Parse(txbp.Text.Trim());
            threshold = float.Parse(txbThreshold.Text.Trim());
            totalTime = float.Parse(txbTotalTime.Text.Trim());
            otimeStep = float.Parse(txbOutTime.Text.Trim()); 
        }
        void initialCaculate()
        {
            //一切数据以LF栅格为基础，包括：1、栅格行列数 2、栅格大小 3、空间坐标系 4、保证对齐(在此之前需要verify)
            IRasterInfo rasterinfo = (LFrasterlayer.Raster as IRawBlocks).RasterInfo;
            cellLength = Convert.ToInt32(rasterinfo.CellSize.X);
            cellLength2 = cellLength * 1.141f;
            rowCount = LFrasterlayer.RowCount;
            columnCount = LFrasterlayer.ColumnCount;
            initialM=new float[rowCount,columnCount];
            arrived = new bool[rowCount, columnCount];
            M = new float[rowCount, columnCount];
            //设置污染物初始分布
            if (CustomMode == false)
            {
                //根据污染物总量、中心位置、半径（像素）生成污染物分布数据
                initialM = fGaussianDistri(Mtotal, PollutantPoint, rOfPollutant); 
            }
            else
            {
                initialM = GISdataManager.Raster2Mat(PollutantDistribution); 
            }
            //污染物分布状态初始化
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    if (initialM[i,j] >= threshold && LF[i,j] == 0)
                    { 
                        M[i, j] = initialM[i, j];
                    }
                }
                
            }
            M = initialM;
            //记录污染物序列索引
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    if (M[i, j] >= threshold)
                    {
                        arrived[i, j] = true;
                        int[] item = new int[2];
                        item[0] = i;
                        item[1] = j;
                        temMIndex.Add(item);
                    }
                }
                
            }
            //根据流速确定模拟步长(计算栅格之间交换的平均时间)
            float[] paras = new float[3];
            paras = fTimestepEvaluation(LF, CD, CV, cellLength, cellLength2);
            timeStep = Convert.ToInt32(Math.Ceiling(paras[2]));



            //最大风速、最大流速的计算
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    if ( LF[i,j] == 1 )
                    {
                        if ( maxCurrentV < CV[i,j] )
                        {
                            maxCurrentV = CV[i, j];
                        }
                        if ( maxWindV < WV[i,j] )
                        {
                            maxWindV = WV[i, j];
                        }
                        
                    }
                }
            }
 
        }
        void startCaculate()
        {       
            this.Invoke(pm.setProcess, new object[] { 0, Convert.ToInt32(totalTime) });
            int time = 0, number = 1;
            float[,] temMMit = new float[rowCount, columnCount];
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    temMMit[i, j] = 0;
                }
                
            }
            //为输出栅格设置计数变量
            for (int i = 0; i < totalTime; i += timeStep)
            {
                this.Invoke(pm.updateProcess, new object[] { "已经计算：" + i + "s;   模拟总时长" + totalTime + "s", i });           
                int count = temMIndex.Count;
                #region
                for (int j = 0; j < count; j++)
                {
                    int[] cording = temMIndex[j];
                    int x = cording[0];
                    int y = cording[1];
                    float temM;
                    if (x >= 1 && x <= rowCount - 2 && y >= 1 && y <= columnCount - 2)
                    {
                        //八方向计算浓度变化
                        //1. i=0,j=0
                        temM = fDiffusion(x, y);//formula跟据想x，y判断列入公式计算
                        if (temM>500)
                        {
                            MessageBox.Show("");
                        }

                            temMMit[x, y] = temM;
                        //2. i=-1,j=-1
                        if (arrived[x - 1,y - 1] == false && LF[x-1,y-1] == 1)
                        {
                            temM = fDiffusion(x - 1, y - 1);
                            arrived[x - 1,y - 1] = true;
                            temMMit[x - 1, y - 1] = temM;
                            int[] temCording = new int[2];
                            temCording[0] = x - 1;
                            temCording[1] = y - 1;
                            temMIndex.Add(temCording);


                        }
                        //3. i=-1,j=0
                        if ( arrived[x - 1, y] == false && LF[x - 1, y] == 1)
                        {
                            temM = fDiffusion(x - 1, y);
                                arrived[x - 1,y] = true;
                                temMMit[x - 1, y] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x - 1;
                                temCording[1] = y;
                                temMIndex.Add(temCording);

                        }
                        //4. i=-1,j=1
                        if ( arrived[x - 1, y + 1] == false && LF[x - 1, y + 1] == 1)
                        {
                            temM = fDiffusion(x - 1, y + 1);
                                arrived[x - 1,y + 1] = true;
                                temMMit[x - 1, y + 1] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x - 1;
                                temCording[1] = y + 1;
                                temMIndex.Add(temCording);

                        }
                        //5. i=0,j=-1
                        if (arrived[x, y - 1] == false && LF[x, y - 1] == 1)
                        {
                            temM = fDiffusion(x, y - 1);
                                arrived[x,y - 1] = true;
                                temMMit[x, y - 1] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x;
                                temCording[1] = y - 1;
                                temMIndex.Add(temCording);
                        }
                        //6. i=0,j=1
                        if (arrived[x, y + 1] == false && LF[x, y + 1] == 1)
                        {
                            temM = fDiffusion(x, y + 1);
                                arrived[x,y + 1] = true;
                                temMMit[x, y + 1] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x;
                                temCording[1] = y + 1;
                                temMIndex.Add(temCording);

                        }
                        //7. i=1,j=-1
                        if (arrived[x + 1, y - 1] == false && LF[x + 1, y - 1] == 1)
                        {
                            temM = fDiffusion(x + 1, y - 1);
                                arrived[x + 1,y - 1] = true;
                                temMMit[x + 1, y - 1] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x + 1;
                                temCording[1] = y - 1;
                                temMIndex.Add(temCording);

                        }
                        //8. i=1,j=0
                        if ( arrived[x + 1, y] == false && LF[x + 1, y] == 1)
                        {
                            temM = fDiffusion(x + 1, y);
                                arrived[x + 1,y] = true;
                                temMMit[x + 1, y] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x + 1;
                                temCording[1] = y;
                                temMIndex.Add(temCording);

                        }
                        //9. i=1,j=1
                        if (arrived[x + 1, y + 1] == false && LF[x + 1, y + 1] == 1)
                        {
                            temM = fDiffusion(x + 1, y + 1);
                                arrived[x + 1,y + 1] = true;
                                temMMit[x + 1, y + 1] = temM;
                                int[] temCording = new int[2];
                                temCording[0] = x + 1;
                                temCording[1] = y + 1;
                                temMIndex.Add(temCording);

                        }
                        
                    }
                    
                }
                #endregion
                M = temMMit;
                if (time >= otimeStep)
                {
                    outM = M;
                    for (int x = 0; x < rowCount; x++)
                        for (int y = 0; y < columnCount; y++)
                        {
                            if (outM[x, y] < threshold)
                            {
                                outM[x, y] = 0;
                            }
                        }                    
                    OutputRaster(SavePollutionDataPath, number);
                    time = 0;
                    number += 1;
                }
                time = time + timeStep;
            }

        }

        //根据污染物总量、中心位置、半径（像素）生成污染物分布数据
        public float[,] fGaussianDistri(float totalMass,IPoint centerpoint,int R)
        {
            double x, y; 
            int rowIndex, colIndex;
            float[,] gaussianDistri = new float[rowCount,columnCount];
            //污染物中心在Mat中的位置
            x = centerpoint.X;
            y = centerpoint.Y;
            //获取出水点在Mit中的位置
            IRaster raster = LFrasterlayer.Raster;
            IRaster2 raster2 = raster as IRaster2;
            colIndex = raster2.ToPixelColumn(x);
            rowIndex = raster2.ToPixelRow(y);
            //设置高斯分布
            for (int i = 0; i < colIndex; i++)
            {
                for (int j = 0; j < rowIndex; j++)
                {
                    gaussianDistri[rowIndex, colIndex] = 0;
                }
            }
            for (int i = -R; i < R; i++)
            {
                for (int j = -R; j < R; j++)
                {
                  float temM = totalMass * (1f / 2f / 3.1415f) * (float)Math.Pow( 2.7182f, -0.5 * ( i * i + j * j));
                  gaussianDistri[rowIndex + i, colIndex + j] = temM;
                }
                
            }

            return gaussianDistri;
 
        }
        //根据流速确定模拟步长(计算栅格之间交换的平均时间)
        public float[] fTimestepEvaluation(float[,] LF, int[,] CD, float[,] CV, float cellLength, float cellLength2)
        {
            //0max，1min，2steptime
            float[] paras = new float[3];
            float min=99999, max=0, steptime;
            float total = 0;
            int count = 0;
            #region
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    if (LF[i,j] == 1)
                    {
                        if (CD[i,j]==1 || CD[i,j]==4 || CD[i,j]==16 || CD[i,j]==64)
                        {
                            float t = cellLength / CV[i, j];
                            if (t>max)
                            {
                                max = t;
                            }
                            if (t<min)
                            {
                                min = t;
                            }
                            total = t + total;
                            count++;
                        }
                        if (CD[i,j]==2 || CD[i,j]==8 || CD[i,j]==32 || CD[i,j]==128)
                        {
                            float t = cellLength2 / CV[i, j];
                              if (t>max)
                            {
                                max = t;
                            }
                            if (t<min)
                            {
                                min = t;
                            }
                            total = t + total;
                            count++;
                        }
                    }
                }

            }
            #endregion
            steptime = total / count;
            paras[0] = max;
            paras[1] = min;
            paras[2] = steptime;
            return paras;
        }
        //浓度扩散公式
        public float fDiffusion(int x, int y)
        {
            float nextstepM;
            float N = 0, S = 0, W = 0, E = 0, NE = 0, NW = 0, SW = 0, SE = 0;
            #region
            if (LF[x,y] == 1)
            {
                switch (CD[x, y])
                {
                    case 1:
                        E += CV[x, y] / maxCurrentV;
                        W -= CV[x, y] / maxCurrentV;
                        break;
                    case 2:
                        SE += CV[x, y] / maxCurrentV;
                        NW -= CV[x, y] / maxCurrentV;
                        break;
                    case 4:
                        S += CV[x, y] / maxCurrentV;
                        N -= CV[x, y] / maxCurrentV;
                        break;
                    case 8:
                        SW += CV[x, y] / maxCurrentV;
                        NE -= CV[x, y] / maxCurrentV;
                        break;
                    case 16:
                        W += CV[x, y] / maxCurrentV;
                        E -= CV[x, y] / maxCurrentV;
                        break;
                    case 32:
                        NW += CV[x, y] / maxCurrentV;
                        SE -= CV[x, y] / maxCurrentV;
                        break;
                    case 64:
                        N += CV[x, y] / maxCurrentV;
                        S -= CV[x, y] / maxCurrentV;
                        break;
                    case 128:
                        NE += CV[x, y] / maxCurrentV;
                        SW -= CV[x, y] / maxCurrentV;
                        break;
                }

                switch (WD[x, y])
                {
                    case 1:
                        E += WV[x, y] / maxWindV;
                        W -= WV[x, y] / maxWindV;
                        break;
                    case 2:
                        SE += WV[x, y] / maxWindV;
                        NW -= WV[x, y] / maxWindV;
                        break;
                    case 4:
                        S += WV[x, y] / maxWindV;
                        N -= WV[x, y] / maxWindV;
                        break;
                    case 8:
                        SW += WV[x, y] / maxWindV;
                        NE -= WV[x, y] / maxWindV;
                        break;
                    case 16:
                        W += WV[x, y] / maxWindV;
                        E -= WV[x, y] / maxWindV;
                        break;
                    case 32:
                        NW += WV[x, y] / maxWindV;
                        SE -= WV[x, y] / maxWindV;
                        break;
                    case 64:
                        N += WV[x, y] / maxWindV;
                        S -= WV[x, y] / maxWindV;
                        break;
                    case 128:
                        NE += WV[x, y] / maxWindV;
                        SW -= WV[x, y] / maxWindV;
                        break;
                }
                nextstepM = M[x, y];

                if (x >= 1 && x <= rowCount - 2 && y >= 1 && y <= columnCount - 2)
                {
                //(1)S方向的元胞受N方向因素影响扩散计算
                if ( LF[x+1,y]==1)
                {
                    nextstepM += m * ( 1 + N )*( M[x + 1, y] - M[x, y]);
                }
                //(2)N方向的元胞受S方向扩散计算
                if ( LF[x-1, y] == 1)
                {
                    float n = M[x + 1, y];
                    nextstepM += m * ( 1 + S )*( M[x - 1, y] - M[x, y]);
                }
                //(3)E方向的元胞受W方向扩散计算
                if ( LF[x, y + 1] == 1)
                {
                    nextstepM += m * ( 1 + W ) * (M[x, y + 1] - M[x, y]);
                }
                //(4)W方向的元胞受E方向扩散计算
                if ( y<=columnCount-2 )
                {
                    nextstepM += m * ( 1 + E )* (M[x, y - 1] - M[x, y]);
                }
                //(5)SE方向的元胞受NW方向扩散计算
                if (LF[ x + 1, y + 1] == 1)
                {
                    nextstepM += d * m * ( 1 + NW ) * (M[x + 1, y + 1] - M[x, y]);
                }
                //(6)NE方向的元胞受SW方向扩散计算
                if (LF[x - 1, y + 1] == 1)
                {
                    nextstepM += d * m * ( 1 +SW )* (M[x - 1, y + 1] - M[x, y]);
                }
                //(7)SW方向的元胞受NE方向扩散计算
                if (LF[x + 1, y - 1] == 1)
                {
                    nextstepM += d * m * ( 1 +NE )* (M[x + 1, y - 1] - M[x, y]);
                }
                //(8)NW方向的元胞受SE方向扩散计算
                if (LF[x - 1, y - 1] == 1)
                {
                    nextstepM += d * m * ( 1 +SE )* (M[x - 1, y - 1] - M[x, y]);
                }
                    
                }

                //蒸散计算
                nextstepM -= p * timeStep * tem;
                return nextstepM;
            }
            #endregion
            return 0f;
        }
        //栅格输出
        void OutputRaster(string outpath, int number)
        {
            string path = outpath + number + ".img";
            IRasterDataset raster = GISdataManager.exportRasterData(path, LFrasterlayer, outM);
        }

        //checkBox单击事件，保证有一项模式被选中，而另一项禁用
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == true)
            {
                checkBox2.Checked = false;
                CustomMode = false;
                txbMtotal.Enabled = true;
                txbR.Enabled = true;
                txbPollutionPostion.Enabled = true;
                btnPollutionPostion.Enabled = true;
                txbPollutantInput.Enabled = false;
                btnPollutantInput.Enabled = false;

            }
            else
            {
                checkBox2.Checked = true;
                CustomMode = true;
                txbMtotal.Enabled = false;
                txbR.Enabled = false;
                txbPollutionPostion.Enabled = false;
                btnPollutionPostion.Enabled = false;
                txbPollutantInput.Enabled = true;
                btnPollutantInput.Enabled = true;
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == true)
            {
                checkBox1.Checked = false;
                CustomMode = true;
                txbMtotal.Enabled = false;
                txbR.Enabled = false;
                txbPollutionPostion.Enabled = false;
                btnPollutionPostion.Enabled = false;
                txbPollutantInput.Enabled = true;
                btnPollutantInput.Enabled = true;
            }
            else
            {
                checkBox1.Checked = true;
                CustomMode = false;
                txbMtotal.Enabled = true;
                txbR.Enabled = true;
                txbPollutionPostion.Enabled = true;
                btnPollutionPostion.Enabled = true;
                txbPollutantInput.Enabled = false;
                btnPollutantInput.Enabled = false;
            }
        }

        //打开数据按钮单击事件，获取数据路径
        private void btnPollutantInput_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入污染物分布数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbPollutantInput.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入污染物分布数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnLandOrRiver_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入河道分布数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbLandOrRiver.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入河道分布数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }

        }

        private void btnWindD_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入风向数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbWindD.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入风向数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnWindV_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入风速数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbWindV.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入风速数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnCurrentD_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入流向数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbCurrentD.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入流向数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnCurrentV_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入流速数据";
                openDialog.Filter = "IMAGINE Image(*.img)|*.img";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路
                    txbCurrentV.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入流速数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnSavePollutionData_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveDlg = new SaveFileDialog();
                saveDlg.Title = "污染扩散数据输出";
                if (saveDlg.ShowDialog() == DialogResult.OK)
                {
                    if (saveDlg.FileName.ToString() == "")
                    {
                        MessageBox.Show("请输入图层名称！", "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                    }
                    else
                    {

                        txbSavePollutionData.Text = saveDlg.FileName.ToString();
                    }
                }
                else
                {
                    return;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据输出错误，请重新计算！", "污染扩散数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnPollutionPostion_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Title = "输入污染物中心位置";
                openDialog.Filter = "Shpfiles(*.shp)|*.shp";
                openDialog.Multiselect = false;
                openDialog.RestoreDirectory = false;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    //得到所加载数据的路径
                    txbPollutionPostion.Text = openDialog.FileName.ToString();
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show("数据加载失败，请重新加载！", "输入污染物中心位置", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            }
        }

        private void btnStartCalculate_Click(object sender, EventArgs e)
        {
            thread = new Thread(new ThreadStart(CaculatePolDiffuse));
            thread.Start();
            this.UseWaitCursor = true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (thread != null)
            {
                thread.Abort();
                this.Invoke(pm.close);
            }
            this.Close();
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

    }
}
