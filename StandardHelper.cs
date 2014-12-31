using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Xml.Linq;
using XMLSerialization;

namespace BASeCamp.XMLSerialization
{
    public class StandardHelper : IXmlPersistableProvider<Point>, IXmlPersistableProvider<PointF>,
        IXmlPersistableProvider<Rectangle>, IXmlPersistableProvider<RectangleF>,
        IXmlPersistableProvider<Image>, IXmlPersistableProvider<System.Int16>, IXmlPersistableProvider<System.Int32>, IXmlPersistableProvider<System.Int64>
        , IXmlPersistableProvider<String>, IXmlPersistableProvider<System.Single>, IXmlPersistableProvider<System.Double>, IXmlPersistableProvider<System.Decimal>,
        IXmlPersistableProvider<System.Boolean>, IXmlPersistableProvider<Color>,
        IXmlPersistableProvider<Font>, IXmlPersistableProvider<ColorMatrix>,
        IXmlPersistableProvider<SolidBrush>, IXmlPersistableProvider<TextureBrush>, IXmlPersistableProvider<LinearGradientBrush>,
        IXmlPersistableProvider<Matrix>, IXmlPersistableProvider<Blend>, IXmlPersistableProvider<ColorBlend>
    {
        public static StandardHelper Static = new StandardHelper();

        #region generic helpers

        private static Dictionary<Type, object> SerializationHelpers = new Dictionary<Type, object>()
        {
            {typeof (Image), Static},
            {typeof (Rectangle), Static},
            {typeof (RectangleF), Static},
            {typeof (Point), Static},
            {typeof (PointF), Static},
            {typeof (Int16), Static},
            {typeof (Int32), Static},
            {typeof (Int64), Static},
            {typeof (Single), Static},
            {typeof (Double), Static},
            {typeof (Decimal), Static},
            {typeof (String), Static},
            {typeof (Font), Static},
            {typeof (ColorMatrix), Static},
            {typeof (Matrix), Static},
            {typeof (SolidBrush), Static},
            {typeof (TextureBrush), Static},
            {typeof (Blend), Static},
            {typeof (ColorBlend), Static}
        };


        public static void AddHelper<T>(IXmlPersistableProvider<T> Provider)
        {
            if (SerializationHelpers.ContainsKey(typeof (T)))
            {
                SerializationHelpers.Remove(typeof (T));
            }
            SerializationHelpers.Add(typeof (T), Provider);
        }

        public static IXmlPersistableProvider<T> GetHelper<T>()
        {
            Type gettype = typeof (T);
            foreach (Type iterate in SerializationHelpers.Keys)
            {
                if (iterate.IsInterface)
                {
                    foreach (Type checkinterface in gettype.GetInterfaces())
                    {
                        if (checkinterface.Equals(gettype))
                            return (IXmlPersistableProvider<T>) SerializationHelpers[iterate];
                    }
                }

                if (iterate.Equals(gettype) || iterate.IsSubclassOf(gettype))
                    return (IXmlPersistableProvider<T>) SerializationHelpers[iterate];
            }
            return null;
        }
        /// <summary>
        /// reads a System.Array from the given XElement.
        /// </summary>
        /// <typeparam name="T">Type of Elements of the Array.</typeparam>
        /// <param name="SourceElement">XElement from which to read Array Data.</param>
        /// <returns>A System.Array populated from the contents of the given XElement.</returns>
        public static System.Array ReadArray<T>(XElement SourceElement)
        {
            //read the "rank" attribute.
            int ArrayRank = SourceElement.GetAttributeInt("Rank");

            int[] DimensionSizes = new int[ArrayRank];
            //now get the Dimensions

            XElement DimensionElement = SourceElement.Element("Dimensions");
            String SBounds = DimensionElement.Attribute("Bounds").Value;
            //split into a string array...
            String[] ArrayBounds = SBounds.Split(',');
            for (int i = 0; i < DimensionSizes.Length; i++)
            {
                if (!int.TryParse(ArrayBounds[i], out DimensionSizes[i]))
                {
                    DimensionSizes[i] = 0;
                }
            }
            //now we have the required dimension sizes, so we can create a System.Array.
            System.Array BuildArray = Array.CreateInstance(typeof (T), DimensionSizes);
            //now we interate through all descendants of the "Dimensions" node.
            int[] elementindex = new int[DimensionSizes.Length];
            foreach (XElement ReadElement in DimensionElement.Descendants("Element"))
            {
                String IndexStr = ReadElement.GetAttributeString("Index");
                String[] IndexStrings = IndexStr.Split(',');
                for (int loopelement = 0; loopelement < elementindex.Length; loopelement++)
                {
                    if (!int.TryParse(IndexStrings[loopelement], out elementindex[loopelement]))
                        elementindex[loopelement] = 0;
                }

                //alright- first, read in this element.
                T readresult = StandardHelper.LoadElement<T>(ReadElement.Descendants().First());
                //once read, assign it to the appropriate array index.
                BuildArray.SetValue((object) readresult, elementindex);
            }
            //assigned successfully- return result.
            return BuildArray;
        }
        /// <summary>
        /// Saves a System.Array into a XElement XML Node with the specified node name and returns the result.
        /// </summary>
        /// <param name="pArrayData">Array Data to save.</param>
        /// <param name="pNodeName">Node name to use.</param>
        /// <exception cref="ArgumentNullException">If either input parameter is null.</exception>
        /// <returns></returns>
        public static XElement SaveArray(System.Array pArrayData, String pNodeName)
        {
            if(pArrayData==null) throw new ArgumentNullException("pArrayData");
            if(pNodeName==null) throw new ArgumentNullException("pNodeName");
            XElement BuildResult = new XElement(pNodeName);
            //dimensions get's saved as a attribute.
            BuildResult.Add(new XAttribute("Rank", pArrayData.Rank));
            //now we have a  Set of "Dimension" Elements, each filled with the elements for that rank.
            int[] indices = new int[pArrayData.Rank];
            int[] lowerbounds = new int[pArrayData.Rank];
            int[] upperbounds = new int[pArrayData.Rank];
            //set to the lower bound of the array.
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = lowerbounds[i] = pArrayData.GetLowerBound(i);
                upperbounds[i] = pArrayData.GetUpperBound(i);
            }
            String BoundString = String.Join(",", from p in upperbounds select (p + 1).ToString());
            XElement DimensionElement = new XElement("Dimensions");
            DimensionElement.Add(new XAttribute("Bounds", BoundString));
            bool SavingDimensions = true;
            while (SavingDimensions)
            {
                //Reflection "magic" is necessary here as far as I can tell; retrieve the SaveElement method of StandardHelper, then build a definition
                //for the Generic Method using the Type of the System.Array Element Type we were passed.
                MethodInfo GenericCall = typeof (StandardHelper).GetMethod("SaveElement").MakeGenericMethod(pArrayData.GetType().GetElementType());

                XElement ElementBuilt = (XElement) GenericCall.Invoke(null, new object[] {pArrayData.GetValue(indices), (Object) "Data"});
                //add the element, tagged with the Indices.
                DimensionElement.Add(new XElement("Element", new XAttribute("Index", String.Join(",", from p in indices select p.ToString())), ElementBuilt));

                //if all indexes are at the upper bound, break the loop.
                bool atMax = true;
                for (int i = 0; i < indices.Length; i++)
                {
                    if (indices[i] < upperbounds[i])
                    {
                        atMax = false;
                        break;
                    }
                }
                if (atMax) break;
             
                //set carry bit.
                //iterate from the first index to the last index.
                //if carry bit is true
                //   if current index is at the maximum value
                //       set index to 0.
                //       set carry bit
                //       next iteration
                //   otherwise, add 1 to current index, set carry bit to false.

                bool fCarry = true;

                for (int i = 0; i < indices.Length; i++)
                {
                    if (fCarry)
                    {
                        if (indices[i] == upperbounds[i])
                        {
                            indices[i] = 0;
                        }
                        else
                        {
                            indices[i]++;
                            fCarry = false;
                        }
                    }
                }
            }
            BuildResult.Add(DimensionElement);
            return BuildResult;
        }

        public static XElement SaveList<T>(List<T> SourceData, String pNodeName)
        {
            //without a Func as in the overload, we'll try to "build" our own function.
            //the function we build will close over a instance of IXMLSerializationProvider (or the type GetXMLData method if it implements IXMLSerializable).
            //basically we want to create the function to handle the loading of classes that implement the interface or have a defined provider.

            Func<T, XElement> buildfunc = (elem) => SaveElement(elem, pNodeName);

            return SaveList<T>(buildfunc, pNodeName, SourceData);
        }

        public static List<T> LoadList<T>(XElement Source)
        {
            Func<XElement, T> constructitem = (xdata) => LoadElement<T>(xdata);
            return LoadList<T>(constructitem, Source);
        }

        public static XElement SaveElement<T>(T SourceData, String pNodeName)
        {
            if (typeof (T).IsArray)
            {
                return SaveArray((System.Array) (Object) SourceData, pNodeName);
            }
            bool implementsInterface = false;
            Func<T, XElement> buildfunc = null;
            foreach (var searchinterface in typeof (T).GetInterfaces())
            {
                if (searchinterface.Equals(typeof (IXmlPersistable)))
                {
                    implementsInterface = true;
                }
            }
            if (implementsInterface)
            {
                //if it implements the interface, we'll use the GetXMLData.
                buildfunc = (elem) => ((IXmlPersistable) elem).GetXmlData(pNodeName);
            }
            else
            {
                //otherwise, let's see if there is an XMLProvider we can use.
                var retrievehelper = GetHelper<T>();
                if (retrievehelper == null)
                {
                }
                else
                {
                    buildfunc = (elem) => retrievehelper.SerializeObject(elem, pNodeName);
                }
            }
            if (buildfunc == null)
            {
                return null;
            }
            return buildfunc(SourceData);
        }

        public static Object LoadElement(Type sTargetType, XElement Source)
        {
            var elementmethod = typeof (StandardHelper).GetMethod("LoadElement", new Type[] {typeof (XElement)});
            var buildcall = elementmethod.MakeGenericMethod(new Type[] {sTargetType});
            Object callresult = buildcall.Invoke(null, new object[] {Source});
            return callresult;
        }

        public static T LoadElement<T>(XElement XMLSource)
        {
            Func<XElement, T> constructitem = null;
            bool implementsInterface = false;
            foreach (var searchinterface in typeof (T).GetInterfaces())
            {
                if (searchinterface.Equals(typeof (IXmlPersistable)))
                {
                    implementsInterface = true;
                }
            }
            if (implementsInterface)
            {
                //if it implements the interface, we'll use the constructor..
                constructitem = (xdata) =>
                {
                    ConstructorInfo ci = typeof (T).GetConstructor(new Type[] {typeof (XElement)});
                    if (ci == null) return default(T);
                    return (T) ci.Invoke(new object[] {xdata});
                };
            }
            else
            {
                //otherwise, let's see if there is an XMLProvider we can use.
                var retrievehelper = GetHelper<T>();
                if (retrievehelper == null)
                {
                    throw new ArgumentException
                        ("Provided type " + typeof (T).Name + " Does not implement IXMLSerializable and does not have a IXMLProvider implementation available.");
                }
                else
                {
                    constructitem = (xdata) => retrievehelper.DeSerializeObject(xdata);
                }
            }
            return constructitem(XMLSource);
        }

        /// <summary>
        /// Saves a list to an XElement.
        /// </summary>
        /// <typeparam name="T">Type of the list to save.</typeparam>
        /// <param name="SourceTransform">Function that takes the Type T item and saves it to an XElement.</param>
        /// <param name="SourceData">Source List data.</param>
        /// <returns></returns>
        public static XElement SaveList<T>(Func<T, XElement> SourceTransform, String pNodeName, List<T> SourceData)
        {
            XElement ListNode = new XElement
                (pNodeName,
                    new XAttribute("ListType", typeof (T).Name));
            foreach (T iterateNode in SourceData)
            {
                XElement addContent = SourceTransform(iterateNode);
                ListNode.Add(addContent);
            }
            return ListNode;
        }

        public static List<T> LoadList<T>(Func<XElement, T> ListLoader, XElement Source)
        {
            List<T> resultlist = new List<T>();
            foreach (XElement child in Source.DescendantNodes())
            {
                T resultnode = ListLoader(child);
                resultlist.Add(resultnode);
            }
            return resultlist;
        }

        #endregion

        #region Point Serialization

        XElement IXmlPersistableProvider<Point>.SerializeObject(Point sourceItem, String pNodeName)
        {
            if (pNodeName == null) pNodeName = "Point";
            return new XElement(pNodeName, new XAttribute("X", sourceItem.X), new XAttribute("Y", sourceItem.Y));
        }

        XElement IXmlPersistableProvider<PointF>.SerializeObject(PointF sourceItem, String pNodeName)
        {
            if (pNodeName == null) pNodeName = "PointF";
            return new XElement(pNodeName, new XAttribute("X", sourceItem.X), new XAttribute("Y", sourceItem.Y));
        }

        #endregion

        #region PointF Serialization

        XElement IXmlPersistableProvider<short>.SerializeObject(short sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<int>.SerializeObject(int sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<long>.SerializeObject(long sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<string>.SerializeObject(string sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<float>.SerializeObject(float sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<double>.SerializeObject(double sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<decimal>.SerializeObject(decimal sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        XElement IXmlPersistableProvider<bool>.SerializeObject(bool sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, new XAttribute("Value", sourceItem));
        }

        public XElement SerializeObject(Color sourceItem, string pNodeName)
        {
            return new XElement
                (pNodeName,
                    new XAttribute("Red", sourceItem.R),
                    new XAttribute("Green", sourceItem.G),
                    new XAttribute("Blue", sourceItem.B),
                    new XAttribute("Alpha", sourceItem.A)
                );
        }

        public XElement SerializeObject(Font sourceItem, string pNodeName)
        {
            XElement result = new XElement
                (pNodeName,
                    new XAttribute("FontFamily", sourceItem.FontFamily),
                    new XAttribute("PointSize", sourceItem.SizeInPoints),
                    new XAttribute("FontStyle", EnumExtensions.ToFlagString(sourceItem.Style)));
            return result;
        }

        public XElement SerializeObject(ColorMatrix sourceItem, string pNodeName)
        {
            float[][] matrixvalues =
            {
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0f, .0f, .0f, .0f, 0}
            };
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    matrixvalues[x][y] = sourceItem[x, y];
                }
            }
            return SaveArray(matrixvalues, pNodeName);
        }


        public XElement SerializeObject(Pen sourceItem, string pNodeName)
        {
            throw new NotImplementedException();
        }

        public XElement SerializeObject(SolidBrush sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, StandardHelper.SaveElement(sourceItem.Color, "Color"));
        }


        public XElement SerializeObject(LinearGradientBrush sourceItem, string pNodeName)
        {
            return new XElement
                (pNodeName,
                    SaveElement(sourceItem.InterpolationColors, "InterpolationColors"),
                    SaveElement(sourceItem.LinearColors, "LinearColors"),
                    SaveElement(sourceItem.Rectangle, "Rectangle"),
                    SaveElement(sourceItem.Transform, "Transform"),
                    SaveElement(sourceItem.Blend, "Blend"),
                    new XAttribute("GammaCorrection", sourceItem.GammaCorrection),
                    new XAttribute("WrapMode", sourceItem.WrapMode)
                );
        }

        LinearGradientBrush IXmlPersistableProvider<LinearGradientBrush>.DeSerializeObject(XElement xmlData)
        {
            ColorBlend interpolationcolors = xmlData.ReadElement<ColorBlend>("InterpolationColors");
            Color[] linearcolors = xmlData.ReadElement<Color[]>("LinearColors");
            RectangleF rectangle = xmlData.ReadElement<RectangleF>("Rectangle");
            Matrix transform = xmlData.ReadElement<Matrix>("Transform");
            Blend blend = xmlData.ReadElement<Blend>("Blend");
            bool gammacorrection = xmlData.GetAttributeBool("GammaCorrection");
            WrapMode wm = (WrapMode) (xmlData.GetAttributeInt("WrapMode"));
            return new LinearGradientBrush(Point.Empty, Point.Empty, Color.Red, Color.Red)
            {
                Blend = blend,
                GammaCorrection = gammacorrection,
                InterpolationColors = interpolationcolors,
                LinearColors = linearcolors,
                Transform = transform,
                WrapMode = wm
            };
        }

        public XElement SerializeObject(ColorBlend sourceItem, string pNodeName)
        {
            return new XElement(pNodeName, SaveElement(sourceItem.Colors, "Colors"), SaveElement(sourceItem.Positions, "Positions"));
        }

        ColorBlend IXmlPersistableProvider<ColorBlend>.DeSerializeObject(XElement xmlData)
        {
            Color[] colors = xmlData.ReadElement<Color[]>("Colors", null);
            float[] positions = xmlData.ReadElement<float[]>("Positions", null);
            ColorBlend cb = new ColorBlend() {Colors = colors, Positions = positions};
            return cb;
        }


        XElement IXmlPersistableProvider<Blend>.SerializeObject(Blend sourceItem, string pNodeName)
        {
            return new XElement
                (pNodeName, StandardHelper.SaveElement(sourceItem.Factors, "Factors"),
                    StandardHelper.SaveElement(sourceItem.Positions, "Positions"));
        }

        Blend IXmlPersistableProvider<Blend>.DeSerializeObject(XElement xmlData)
        {
            float[] Factors = xmlData.ReadElement<float[]>("Factors", null);
            float[] Positions = xmlData.ReadElement<float[]>("Positions", null);


            Blend b = new Blend() {Factors = Factors, Positions = Positions};
            return b;
        }


        public XElement SerializeObject(Matrix sourceItem, string pNodeName)
        {
            return new XElement
                (pNodeName,
                    StandardHelper.SaveArray(sourceItem.Elements, "Elements"));
        }

        Matrix IXmlPersistableProvider<Matrix>.DeSerializeObject(XElement xmlData)
        {
            float[] elementread = (float[]) StandardHelper.ReadArray<float>(xmlData.Element("Elements"));
            Matrix m = new Matrix(elementread[0], elementread[1], elementread[2], elementread[3], elementread[4], elementread[5]);
            return m;
        }


        TextureBrush IXmlPersistableProvider<TextureBrush>.DeSerializeObject(XElement xmlData)
        {
            Image buildimage = xmlData.ReadElement<Image>("TextureImage", null);
            Matrix transformmatrix = xmlData.ReadElement<Matrix>("Transform", null);
            WrapMode texturewrap = (WrapMode) xmlData.GetAttributeInt("WrapMode");
            TextureBrush buildbrush = new TextureBrush(buildimage);
            buildbrush.Transform = transformmatrix;
            buildbrush.WrapMode = texturewrap;
            return buildbrush;
        }

        public XElement SerializeObject(TextureBrush sourceItem, string pNodeName)
        {
            XElement result = new XElement
                (pNodeName,
                    StandardHelper.SaveElement(sourceItem.Image, "TextureImage"),
                    StandardHelper.SaveElement(sourceItem.Transform, "Transform"),
                    new XAttribute("WrapMode", (int) sourceItem.WrapMode));


            return result;
        }

        SolidBrush IXmlPersistableProvider<SolidBrush>.DeSerializeObject(XElement xmlData)
        {
            Color useColor = xmlData.ReadElement("Color", Color.Transparent);
            return new SolidBrush(useColor);
        }


        ColorMatrix IXmlPersistableProvider<ColorMatrix>.DeSerializeObject(XElement xmlData)
        {
            Array result = ReadArray<float[]>(xmlData);
            float[][] usematrix = (float[][]) result;
            return new ColorMatrix(usematrix);
        }

        Font IXmlPersistableProvider<Font>.DeSerializeObject(XElement xmlData)
        {
            String FontFamily = xmlData.GetAttributeString("FontFamily");
            float PointSize = xmlData.GetAttributeFloat("PointSize", 12f);
            String FontStyle = xmlData.GetAttributeString("FontStyle", "Regular");
            FontStyle fs = EnumExtensions.FromFlagString<FontStyle>(FontStyle);

            return new Font(FontFamily, PointSize, fs);
        }

        public Color DeSerializeObject(XElement xmlData)
        {
            int Red = int.Parse(xmlData.Attribute("Red").Value);
            int Green = int.Parse(xmlData.Attribute("Red").Value);
            int Blue = int.Parse(xmlData.Attribute("Blue").Value);
            int Alpha = int.Parse(xmlData.Attribute("Alpha").Value);
            return Color.FromArgb(Alpha, Red, Green, Blue);
        }

        bool IXmlPersistableProvider<bool>.DeSerializeObject(XElement xmlData)
        {
            return bool.Parse(xmlData.Attribute("Value").Value);
        }

        decimal IXmlPersistableProvider<decimal>.DeSerializeObject(XElement xmlData)
        {
            return decimal.Parse(xmlData.Attribute("Value").Value);
        }

        double IXmlPersistableProvider<double>.DeSerializeObject(XElement xmlData)
        {
            return double.Parse(xmlData.Attribute("Value").Value);
        }

        float IXmlPersistableProvider<float>.DeSerializeObject(XElement xmlData)
        {
            return float.Parse(xmlData.Attribute("Value").Value);
        }

        string IXmlPersistableProvider<string>.DeSerializeObject(XElement xmlData)
        {
            return xmlData.Attribute("Value").Value;
        }

        long IXmlPersistableProvider<long>.DeSerializeObject(XElement xmlData)
        {
            return long.Parse(xmlData.Attribute("Value").Value);
        }

        int IXmlPersistableProvider<int>.DeSerializeObject(XElement xmlData)
        {
            throw new NotImplementedException();
        }

        short IXmlPersistableProvider<short>.DeSerializeObject(XElement xmlData)
        {
            throw new NotImplementedException();
        }

        PointF IXmlPersistableProvider<PointF>.DeSerializeObject(XElement xmlData)
        {
            float X = 0, Y = 0;
            String sX, sY;
            sX = sY = "0";
            foreach (XAttribute LookAttribute in xmlData.Attributes())
            {
                if (LookAttribute.Name == "X")
                    sX = LookAttribute.Value;
                else if (LookAttribute.Name == "Y")
                    sY = LookAttribute.Value;
            }
            X = Single.Parse(sX);
            Y = Single.Parse(sY);
            return new PointF(X, Y);
        }


        Point IXmlPersistableProvider<Point>.DeSerializeObject(XElement xmlData)
        {
            int X = 0, Y = 0;
            String sX, sY;
            sX = sY = "0";
            foreach (XAttribute LookAttribute in xmlData.Attributes())
            {
                if (LookAttribute.Name == "X")
                    sX = LookAttribute.Value;
                else if (LookAttribute.Name == "Y")
                    sY = LookAttribute.Value;
            }
            X = Int32.Parse(sX);
            Y = Int32.Parse(sY);
            return new Point(X, Y);
        }

        #endregion

        #region Rectangle Serialization

        XElement IXmlPersistableProvider<Rectangle>.SerializeObject(Rectangle sourceItem, String pNodeName)
        {
            if (pNodeName == null) pNodeName = "Rectangle";
            return new XElement
                ("Rectangle",
                    new XAttribute("Left", sourceItem.Left), new XAttribute("Top", sourceItem.Top), new XAttribute("Width", sourceItem.Width),
                    new XAttribute("Height", sourceItem.Height));
        }

        Rectangle IXmlPersistableProvider<Rectangle>.DeSerializeObject(XElement xmlData)
        {
            int Left = 0, Top = 0, Width = 0, Height = 0;
            String sLeft, sTop, sWidth, sHeight;
            sLeft = sTop = sWidth = sHeight = "0";
            foreach (XAttribute LookAttribute in xmlData.Attributes())
            {
                if (LookAttribute.Name == "Left")
                    sLeft = LookAttribute.Value;
                else if (LookAttribute.Name == "Top")
                    sTop = LookAttribute.Value;
                else if (LookAttribute.Name == "Width")
                    sWidth = LookAttribute.Value;
                else if (LookAttribute.Name == "Height")
                    sHeight = LookAttribute.Value;
            }
            Left = Int32.Parse(sLeft);
            Top = Int32.Parse(sTop);
            Width = Int32.Parse(sWidth);
            Height = Int32.Parse(sHeight);
            return new Rectangle(Left, Top, Width, Height);
        }

        #endregion

        #region RectangleF deserialization

        XElement IXmlPersistableProvider<RectangleF>.SerializeObject(RectangleF sourceItem, String pNodeName)
        {
            if (pNodeName == null) pNodeName = "Rectangle";
            return new XElement
                ("Rectangle",
                    new XAttribute("Left", sourceItem.Left), new XAttribute("Top", sourceItem.Top), new XAttribute("Width", sourceItem.Width),
                    new XAttribute("Height", sourceItem.Height));
        }

        RectangleF IXmlPersistableProvider<RectangleF>.DeSerializeObject(XElement xmlData)
        {
            float Left = 0, Top = 0, Width = 0, Height = 0;
            String sLeft, sTop, sWidth, sHeight;
            sLeft = sTop = sWidth = sHeight = "0";
            foreach (XAttribute LookAttribute in xmlData.Attributes())
            {
                if (LookAttribute.Name == "Left")
                    sLeft = LookAttribute.Value;
                else if (LookAttribute.Name == "Top")
                    sTop = LookAttribute.Value;
                else if (LookAttribute.Name == "Width")
                    sWidth = LookAttribute.Value;
                else if (LookAttribute.Name == "Height")
                    sHeight = LookAttribute.Value;
            }
            Left = Single.Parse(sLeft);
            Top = Single.Parse(sTop);
            Width = Single.Parse(sWidth);
            Height = Single.Parse(sHeight);
            return new RectangleF(Left, Top, Width, Height);
        }

        #endregion

        #region Image implementation

        XElement IXmlPersistableProvider<Image>.SerializeObject(Image sourceItem, String pNodeName)
        {
            if (pNodeName == null) pNodeName = "Image";
            //an image. we'll do a bit of a cheat here, and base64 encode the image data as the value of the XML tag.
            //Convert.ToBase64String(SourceItem.)
            //to get the bytes, we'll save the image to a MemoryStream.
            String sBase64 = null;
            using (MemoryStream ms = new MemoryStream())
            {
                sourceItem.Save(ms, ImageFormat.Png);
                byte[] buff = ms.GetBuffer();
                sBase64 = Convert.ToBase64String(buff);
            }
            return new XElement(pNodeName, sBase64);
        }

        Image IXmlPersistableProvider<Image>.DeSerializeObject(XElement xmlData)
        {
            String sBase64 = xmlData.Value;
            byte[] contents = Convert.FromBase64String(sBase64);
            Image result = null;
            result = Image.FromStream(new MemoryStream(contents));
            return result;
        }

        #endregion
    }


    public static class XElementExtensions
    {
        public static String GetAttributeString(this XElement src, String pName, String pDefault = null)
        {
            var GrabAttribute = src.Attribute(pName);
            if (GrabAttribute != null)
            {
                return GrabAttribute.Value;
            }
            return pDefault;
        }

        public static float GetAttributeFloat(this XElement src, String pName, float pDefault = 0)
        {
            try
            {
                return float.Parse(src.GetAttributeString(pName, pDefault.ToString()));
            }
            catch (Exception exx)
            {
                return pDefault;
            }
        }

        public static double GetAttributeDouble(this XElement src, String pName, double pDefault = 0)
        {
            try
            {
                return Double.Parse(src.GetAttributeString(pName, pDefault.ToString()));
            }
            catch (Exception exx)
            {
                return pDefault;
            }
        }

        public static int GetAttributeInt(this XElement src, String pName, int pDefault = 0)
        {
            try
            {
                return int.Parse(src.GetAttributeString(pName, pDefault.ToString()));
            }
            catch (Exception exx)
            {
                return pDefault;
            }
        }

        public static long GetAttributeLong(this XElement src, String pName, long pDefault = 0)
        {
            try
            {
                return long.Parse(src.GetAttributeString(pName, pDefault.ToString()));
            }
            catch (Exception exx)
            {
                return pDefault;
            }
        }

        public static bool GetAttributeBool(this XElement src, String pName, bool pDefault = false)
        {
            return GetAttributeInt(src, pName, pDefault ? 1 : 0) == 1;
        }

        public static Object ReadElement(this XElement src, Type ReadType, String pElementName, Object Default = null)
        {
            XElement NodeCheck = src.Element(pElementName);
            if (NodeCheck == null) return Default;
            return StandardHelper.LoadElement(ReadType, src);
        }

        public static T ReadElement<T>(this XElement src, String pElementName, T Default = default(T))
        {
            XElement NodeCheck = src.Element(pElementName);
            if (NodeCheck == null) return Default;
            return StandardHelper.LoadElement<T>(NodeCheck);
        }
    }
}