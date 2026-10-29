using Schuly.API.Linking;

namespace Schuly.Tests
{
    public class SvgSanitizerTests
    {
        private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\"";

        [Test]
        public async Task A_plain_icon_survives()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns} viewBox=\"0 0 24 24\"><g fill=\"none\" stroke=\"#000\"><path d=\"M1 1h2\"/><circle cx=\"1\" cy=\"1\" r=\"2\"/></g></svg>");
            await Assert.That(clean).IsNotNull();
            await Assert.That(clean!.Contains("<path")).IsTrue();
            await Assert.That(clean.Contains("viewBox")).IsTrue();
        }

        [Test]
        public async Task Script_elements_are_dropped()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns}><script>alert(1)</script><path d=\"M0 0\"/></svg>");
            await Assert.That(clean!.Contains("script")).IsFalse();
            await Assert.That(clean.Contains("alert")).IsFalse();
        }

        [Test]
        public async Task Event_handler_attributes_are_dropped()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns} onload=\"alert(1)\"><path d=\"M0 0\" onclick=\"x()\" onmouseover=\"x()\"/></svg>");
            await Assert.That(clean).IsNotNull();
            await Assert.That(clean!.Contains("onload")).IsFalse();
            await Assert.That(clean.Contains("onclick")).IsFalse();
            await Assert.That(clean.Contains("onmouseover")).IsFalse();
        }

        [Test]
        public async Task Foreign_object_and_its_html_are_dropped()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns}><foreignObject><div xmlns=\"http://www.w3.org/1999/xhtml\">hi</div></foreignObject><path d=\"M0 0\"/></svg>");
            await Assert.That(clean!.Contains("foreignObject")).IsFalse();
            await Assert.That(clean.Contains("div")).IsFalse();
        }

        [Test]
        public async Task External_and_script_hrefs_are_dropped_but_fragments_stay()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns} xmlns:xlink=\"http://www.w3.org/1999/xlink\"><defs><path id=\"p\" d=\"M0 0\"/></defs><use href=\"https://evil.example/x.svg#a\"/><use xlink:href=\"javascript:alert(1)\"/><use href=\"#p\"/><use xlink:href=\"#p\"/></svg>");
            await Assert.That(clean!.Contains("evil.example")).IsFalse();
            await Assert.That(clean.Contains("javascript")).IsFalse();
            await Assert.That(clean.Contains("href=\"#p\"")).IsTrue();
        }

        [Test]
        public async Task Image_and_style_elements_are_dropped()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns}><image href=\"https://evil.example/a.png\"/><style>path{{fill:url(https://evil.example/x)}}</style><path d=\"M0 0\"/></svg>");
            await Assert.That(clean!.Contains("image")).IsFalse();
            await Assert.That(clean.Contains("style")).IsFalse();
            await Assert.That(clean.Contains("evil")).IsFalse();
        }

        [Test]
        public async Task External_urls_in_attributes_are_dropped_but_local_references_stay()
        {
            var clean = SvgSanitizer.Sanitize($"<svg {Ns}><defs><linearGradient id=\"g\"/></defs><path d=\"M0 0\" fill=\"url(#g)\" stroke=\"url(https://evil.example/x)\" style=\"fill:url('http://evil.example/y')\"/></svg>");
            await Assert.That(clean!.Contains("evil")).IsFalse();
            await Assert.That(clean.Contains("url(#g)")).IsTrue();
        }

        [Test]
        public async Task Doctypes_with_entities_are_rejected()
        {
            var clean = SvgSanitizer.Sanitize($"<!DOCTYPE svg [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><svg {Ns}><title>&x;</title></svg>");
            await Assert.That(clean).IsNull();
        }

        [Test]
        public async Task Non_svg_roots_malformed_and_oversized_markup_are_rejected()
        {
            await Assert.That(SvgSanitizer.Sanitize("<html><body/></html>")).IsNull();
            await Assert.That(SvgSanitizer.Sanitize("<svg")).IsNull();
            await Assert.That(SvgSanitizer.Sanitize(null)).IsNull();
            await Assert.That(SvgSanitizer.Sanitize($"<svg {Ns}><desc>{new string('a', SvgSanitizer.MaxBytes)}</desc></svg>")).IsNull();
        }
    }
}
