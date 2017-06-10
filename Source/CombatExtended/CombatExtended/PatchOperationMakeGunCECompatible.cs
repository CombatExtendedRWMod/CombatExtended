using System;
using System.Xml;

using Verse;

namespace CombatExtended
{
	public class PatchOperationMakeGunCECompatible : PatchOperation
	{
		public string defName;
        public XmlContainer statBases;
		public XmlContainer Properties;
		public XmlContainer AmmoUser;
		public XmlContainer FireModes;
		public XmlContainer weaponTags;
        public XmlContainer costList;

		protected override bool ApplyWorker (XmlDocument xml)
		{
			bool result = false;

			if (defName.NullOrEmpty()) {
				return false;
			}

			foreach (var current in xml.SelectNodes ("*/ThingDef[defName=\"" + defName + "\"]")) {
				result = true;

				var xmlNode = current as XmlNode;

                if (statBases?.node.HasChildNodes ?? false)
                {
                    AddOrReplaceStatBases(xml, xmlNode);
                }
                if (costList?.node.HasChildNodes ?? false)
                {
                    AddOrReplaceCostList(xml, xmlNode);
                }

				if (Properties != null && Properties.node.HasChildNodes) {
					AddOrReplaceVerbPropertiesCE (xml, xmlNode);
				}

				if (AmmoUser != null || FireModes != null) {
					AddOrReplaceCompsCE (xml, xmlNode);
				}

				if (weaponTags != null && weaponTags.node.HasChildNodes) {
					AddOrReplaceWeaponTags (xml, xmlNode);
				}
			}

			return result;
		}

		private bool GetOrCreateNode(XmlDocument xml, XmlNode xmlNode, string name, out XmlElement output) {
			var comps_nodes = xmlNode.SelectNodes (name);
			if (comps_nodes.Count == 0) {
				output = xml.CreateElement (name);
				xmlNode.AppendChild (output);
				return false;
			} else {
				output = comps_nodes [0] as XmlElement;
				return true;
			}
		}

		private XmlElement CreateListElementAndPopulate (XmlDocument xml, XmlNode reference, string type = null)
		{
			var element = xml.CreateElement ("li");
			if (type != null) {
				element.SetAttribute ("Class", type);
			}

			Populate (xml, reference, ref element);

			return element;
		}

		private void Populate(XmlDocument xml, XmlNode reference, ref XmlElement destination, bool overrideExisting = false)
        {
            foreach (XmlNode current in reference)
            {
                if (overrideExisting)
                {
                    var existingNode = destination.SelectSingleNode(current.Name);
                    if (existingNode != null && existingNode.Value != null)
                    {
                        existingNode.Value = current.Value;
                        continue;
                    }
                }
                destination.AppendChild(xml.ImportNode(current, true));
            }
        }

		private void AddOrReplaceVerbPropertiesCE (XmlDocument xml, XmlNode xmlNode)
		{
			XmlElement verbs;
			if (GetOrCreateNode(xml, xmlNode, "verbs", out verbs)) {
				// remove Verb_Shoot
				var verb_shoot_nodes = verbs.SelectNodes ("li[verbClass=\"Verb_Shoot\" or verbClass=\"Verb_ShootOneUse\" or verbClass=\"Verb_LaunchProjectile\"]");
				foreach (var verb_shoot_current in verb_shoot_nodes) {
					var verb_shoot = verb_shoot_current as XmlNode;
					if (verb_shoot != null) {
						verbs.RemoveChild (verb_shoot);
					}
				}
			}

			verbs.AppendChild (CreateListElementAndPopulate (xml, this.Properties.node, "CombatExtended.VerbPropertiesCE"));
		}

		private void AddOrReplaceCompsCE (XmlDocument xml, XmlNode xmlNode)
		{
			XmlElement comps;
			GetOrCreateNode (xml, xmlNode, "comps", out comps);

			// add CompProperties_AmmoUser
			if (AmmoUser != null) {
				comps.AppendChild (CreateListElementAndPopulate (xml, AmmoUser.node, "CombatExtended.CompProperties_AmmoUser"));
			}

			// add CompProperties_FireModes
			if (FireModes != null) {
				comps.AppendChild (CreateListElementAndPopulate (xml, FireModes.node, "CombatExtended.CompProperties_FireModes"));
			}
		}

		private void AddOrReplaceWeaponTags(XmlDocument xml, XmlNode xmlNode) {
			XmlElement weaponTagsElement;
			GetOrCreateNode (xml, xmlNode, "weaponTags", out weaponTagsElement);

			Populate(xml, this.weaponTags.node, ref weaponTagsElement);
		}

		private void AddOrReplaceStatBases(XmlDocument xml, XmlNode xmlNode) {
			XmlElement statBasesElement;
			GetOrCreateNode (xml, xmlNode, "statBases", out statBasesElement);

            // Remove unused vanilla stats
            if (statBasesElement.HasChildNodes)
            {
                var vanillaStats = statBasesElement.SelectNodes("AccuracyTouch | AccuracyShort | AccuracyMedium | AccuracyLong");
                foreach(XmlNode cur in vanillaStats)
                {
                    statBasesElement.RemoveChild(cur);
                }
            }

			Populate(xml, statBases.node, ref statBasesElement, true);
        }

        private void AddOrReplaceCostList(XmlDocument xml, XmlNode xmlNode)
        {
            XmlElement costListElement;
            GetOrCreateNode(xml, xmlNode, "costList", out costListElement);

            // Clear list first
            if (costListElement.HasChildNodes)
            {
                costListElement.RemoveAll();
            }

            Populate(xml, costList.node, ref costListElement);
        }
    }
}