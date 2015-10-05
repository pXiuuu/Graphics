using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEditor.Graphs.Material
{
	[Title("Output/Pixel Shader")]
	class PixelShaderNode : BaseMaterialNode, IGeneratesBodyCode
	{
		private const string kAlbedoSlotName = "Albedo";
		private const string kNormalSlotName = "Normal";
		private const string kEmissionSlotName = "Emission";
		private const string kMetallicSlotName = "Metallic";
		private const string kSmoothnessSlotName = "Smoothness";
		private const string kOcclusion = "Occlusion";
		private const string kAlphaSlotName = "Alpha";

		[SerializeField]
		private string m_LightFunction;

		private static List<BaseLightFunction> s_LightFunctions;

		public override void Init()
		{
			name = "PixelMaster";
			base.Init ();

			AddSlot (new Slot(SlotType.InputSlot, kAlbedoSlotName));
			AddSlot (new Slot(SlotType.InputSlot, kNormalSlotName));
			AddSlot (new Slot(SlotType.InputSlot, kEmissionSlotName));
			AddSlot (new Slot(SlotType.InputSlot, kMetallicSlotName));
			AddSlot (new Slot(SlotType.InputSlot, kSmoothnessSlotName));
			AddSlot (new Slot(SlotType.InputSlot, kOcclusion));
			AddSlot (new Slot(SlotType.InputSlot, kAlphaSlotName));
		}

		private static List<BaseLightFunction> GetLightFunctions ()
		{
			if (s_LightFunctions == null)
			{
				s_LightFunctions = new List<BaseLightFunction> ();

				foreach (Type type in Assembly.GetAssembly (typeof(BaseLightFunction)).GetTypes ())
				{
					if (type.IsClass && !type.IsAbstract && (type.IsSubclassOf (typeof(BaseLightFunction))))
					{
						var func = Activator.CreateInstance (type) as BaseLightFunction;
						s_LightFunctions.Add (func);
					}
				}
			}
			return s_LightFunctions;
		}
		
		public virtual void GenerateLightFunction (ShaderGenerator visitor)
		{
			visitor.AddPragmaChunk (m_LightFunction);

			var lightFunction = GetLightFunctions().FirstOrDefault(x => x.GetName() == m_LightFunction);
			int lightFuncIndex = 0;
			if (lightFunction != null)
				lightFuncIndex = GetLightFunctions ().IndexOf (lightFunction);

			if (lightFuncIndex < s_LightFunctions.Count)
			{
				BaseLightFunction func = s_LightFunctions[lightFuncIndex];
				func.GenerateBody (visitor);
			}
		}

		public void GenerateNodeCode(ShaderGenerator shaderBody, GenerationMode generationMode)
		{
			// do the normal slot first so that it can be used later in the shader :)
			var normal = FindInputSlot (kNormalSlotName);
			var nodes = new List<BaseMaterialNode>();
			CollectChildNodesByExecutionOrder(nodes, normal, false);

			foreach (var node in nodes)
			{
				if (node is IGeneratesBodyCode)
					(node as IGeneratesBodyCode).GenerateNodeCode (shaderBody, generationMode);
			}

			foreach (var edge in normal.edges)
			{
				var node = edge.fromSlot.node as BaseMaterialNode;
				shaderBody.AddShaderChunk("o." + normal.name + " = " + node.GetOutputVariableNameForSlot(edge.fromSlot, generationMode) + ";", true);
			}

			// track the last index of nodes... they have already been processed :)
			int pass2StartIndex = nodes.Count;

			//Get the rest of the nodes for all the other slots
			CollectChildNodesByExecutionOrder(nodes, null, false);
			for (var i = pass2StartIndex; i < nodes.Count; i++)
			{
				var node = nodes[i];
				if (node is IGeneratesBodyCode)
					(node as IGeneratesBodyCode).GenerateNodeCode(shaderBody, generationMode);
			}

			foreach (var slot in slots)
			{
				if (slot == normal)
					continue;

				foreach (var edge in slot.edges)
				{
					var node = edge.fromSlot.node as BaseMaterialNode;
					shaderBody.AddShaderChunk("o." + slot.name + " = " + node.GetOutputVariableNameForSlot(edge.fromSlot, generationMode) + ";", true);
				}
			}
		}

		public override string GetOutputVariableNameForSlot(Slot s, GenerationMode generationMode)
		{
			return GetOutputVariableNameForNode();
		}

		public override void NodeUI (Graphs.GraphGUI host)
		{
			base.NodeUI(host);
			var lightFunction = GetLightFunctions ().FirstOrDefault (x => x.GetName () == m_LightFunction);
			int lightFuncIndex = 0;
			if (lightFunction != null)
				lightFuncIndex = GetLightFunctions ().IndexOf (lightFunction);
			lightFuncIndex = EditorGUILayout.Popup (lightFuncIndex, s_LightFunctions.Select(x => x.GetName ()).ToArray (), EditorStyles.popup);
			m_LightFunction = GetLightFunctions ()[lightFuncIndex].GetName ();
		}
	}
}
