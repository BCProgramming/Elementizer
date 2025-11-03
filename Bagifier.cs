using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BASeCamp.Elementizer
{
    //a propertybag is a generic element that is passed through the Bagifier save/restore for read/writing elements.
    //The idea is that a propertybag "tree" could be serialized and deserialized to various types of elements (XML, json, yaml, etc).
    
    
    public class PropertyBag
    {
        private Dictionary<String, Object> BagContents = new Dictionary<string, object>();

        public void Add(String Name, Object Item)
        {
            BagContents[Name] = Item;
        }
        public Object? Get(String ItemName,Object defaultvalue = null)
        {
            return BagContents?[ItemName] ?? defaultvalue;
        }




    }

    public interface IBagSerializable
    {
        //expects constructor:

        //serializes the contents to this PropertyBag.
        void SerializeBag(PropertyBag Target, Object context);

        //expects constructor (PropertyBag Source, Object Context)

    }
    //we will need to kind of recreate all the special helpers we already made for Elementizer itself. That's a little annoying.
    public class SerialBagHelper
    {
    }
}
