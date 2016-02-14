using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace BASeCamp.Elementizer
{
    /// <summary>
    /// Indicates a object supports our XElement Serialization, serializing to an XElement.
    /// This also means the class must implement a constructor which accepts a single XElement parameter
    /// and will reconstruct the object. Classes implementing this interface should also implement a constructor that accepts a XElement parameter.
    /// </summary>
    public interface IXmlPersistable
    {
        /// <summary>
        /// Retrieves the XElement representation of this class instance.
        /// </summary>
        /// <returns>XElement representing this class which can be used to reconstitute it by passing it via the constructor.</returns>
        XElement GetXmlData(String pNodeName);

    }
    /// <summary>
    /// Class implementation that is used to provide serialization and deserialization to and from an XElement
    /// for a given class.
    /// </summary>
    public interface IXmlPersistableProvider<T>
    {
        /// <summary>
        /// Serializes the given object to a XElement instance.
        /// </summary>
        /// <param name="sourceItem">Source Item to serialize to an XML Element.</param>
        /// <param name="pNodeName">Name to give the resulting XML Node.</param>
        /// <returns>XElement representing the given instance.</returns>
        XElement SerializeObject(T sourceItem,String pNodeName);
        /// <summary>
        /// constructs a T out of the given XElement data.
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns>reconstuted type T from the given XElement.</returns>
        T DeSerializeObject(XElement xmlData);

    }
    public static class SerializationProviderExtensions
    {
        public static XElement SerializeObject<T>(this IXmlPersistableProvider<T> source,String pNodeName,T SourceItem)
        {
            return source.SerializeObject(SourceItem, pNodeName);
        }
    }
}
