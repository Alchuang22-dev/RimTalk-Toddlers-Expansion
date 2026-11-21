using System.Xml;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.AI
{
	public sealed class PatchOperation_PrependToddlerPickup : PatchOperationPathed
	{
		protected override bool ApplyWorker(XmlDocument xml)
		{
			if (!(xml.SelectSingleNode(xpath) is XmlElement original))
			{
				return false;
			}

			XmlElement pickup = xml.CreateElement("li");
			pickup.SetAttribute("Class", typeof(JobGiver_PickUpUncarriedToddler).FullName);
			string originalClass = original.GetAttribute("Class");
			if (originalClass == nameof(ThinkNode_Priority) || originalClass == typeof(ThinkNode_Priority).FullName)
			{
				XmlElement existingChildren = original["subNodes"] ?? (XmlElement)original.AppendChild(xml.CreateElement("subNodes"));
				existingChildren.PrependChild(pickup);
				return true;
			}

			XmlElement wrapper = xml.CreateElement(original.Name);
			wrapper.SetAttribute("Class", nameof(ThinkNode_Priority));
			XmlElement children = xml.CreateElement("subNodes");
			wrapper.AppendChild(children);
			children.AppendChild(pickup);

			XmlElement exit = xml.CreateElement("li");
			foreach (XmlAttribute attribute in original.Attributes)
			{
				exit.Attributes.Append((XmlAttribute)attribute.CloneNode(true));
			}

			while (original.FirstChild != null)
			{
				exit.AppendChild(original.FirstChild);
			}

			children.AppendChild(exit);
			original.ParentNode.ReplaceChild(wrapper, original);
			return true;
		}
	}
}
