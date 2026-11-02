using System.Linq;
using System.Reflection;
using Godmist.Data.Skills;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class SkillAvailabilityVisualTests
    {
        private static T Field<T>(object owner,string name)=>(T)owner.GetType()
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);

        [Test]
        public void UnavailableShaderIsRetainedForPlayersAndSupportsMaskedUi()
        {
            var shader=Resources.Load<Shader>("Godmist/UnavailableSkillIcon");
            Assert.That(shader,Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(shader),Does.Contain("/Resources/"));
            Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
            var material=new Material(shader);
            try
            {
                foreach(var property in new[]{"_MainTex","_Color","_Stencil","_StencilComp","_StencilReadMask","_StencilWriteMask","_ColorMask","_UseUIAlphaClip"})
                    Assert.That(material.HasProperty(property),Is.True,property);
                Assert.That(material.GetFloat("_Brightness"),Is.EqualTo(.7f).Within(.001f));
            }
            finally {Object.DestroyImmediate(material);}
        }

        [TestCase(false)] [TestCase(true)]
        public void OnlyIconIsDesaturatedAndMaterialRestoresWithoutChangingFramesOrShortcuts(bool activeToggle)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatSkillSlot.prefab");
            var root=Object.Instantiate(prefab);
            Material disabled=null;
            try
            {
                var slot=root.GetComponent<CombatSkillSlotView>();
                slot.Initialize(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorCut.asset"),null,null);
                slot.SetShortcut(0);
                var icon=Field<Image>(slot,"icon");var overlay=Field<Image>(slot,"unavailableOverlay");
                var shortcut=Field<TextMeshProUGUI>(slot,"shortcutText");
                var original=icon.material;var sprite=icon.sprite;var text=shortcut.text;var color=shortcut.color;
                var otherImages=root.GetComponentsInChildren<Image>(true).Where(i=>i!=icon && i!=overlay)
                    .Select(i=>(Image:i,Material:i.material,Color:i.color)).ToArray();
                slot.RefreshAvailability(false,600,activeToggle,string.Empty);
                disabled=icon.material;
                Assert.That(disabled.shader.name,Is.EqualTo("Godmist/UI/Unavailable Skill Icon"));
                Assert.That(icon.sprite,Is.SameAs(sprite));
                Assert.That(overlay.enabled,Is.True);
                Assert.That(shortcut.text,Is.EqualTo(text));Assert.That(shortcut.color,Is.EqualTo(color));
                Assert.That(shortcut.gameObject.activeSelf,Is.True);
                foreach(var other in otherImages)
                { Assert.That(other.Image.material,Is.SameAs(other.Material)); Assert.That(other.Image.color,Is.EqualTo(other.Color)); }
                slot.RefreshAvailability(true,600,activeToggle,string.Empty);
                Assert.That(icon.material,Is.SameAs(original));
                Assert.That(overlay.enabled,Is.False);
                Assert.That(icon.color,Is.EqualTo(activeToggle ? new Color(.35f,1,.65f,1) : Color.white));
                slot.RefreshAvailability(false,600,activeToggle,string.Empty);
                Assert.That(icon.material,Is.SameAs(disabled),"Pooled slots reuse their material instead of allocating per frame.");
                // Consumable slots use Initialize(null) before assigning the item
                // sprite. They must keep their existing appearance when pooled.
                slot.Initialize(null,null,null);
                slot.RefreshAvailability(false,600,false,string.Empty);
                Assert.That(icon.material,Is.SameAs(original),"The skill-only style must not affect item slots.");
            }
            finally
            {
                // A plain MonoBehaviour's lifecycle is not dispatched in EditMode.
                // Exercise the same cleanup explicitly; Play Mode uses real teardown.
                typeof(CombatSkillSlotView).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(root.GetComponent<CombatSkillSlotView>(),null);
                Object.DestroyImmediate(root);
            }
            Assert.That(disabled==null,Is.True,"Destroying the slot must release its runtime material.");
        }
    }
}
