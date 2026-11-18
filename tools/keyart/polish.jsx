// Photoshop polish pass on the open capsule PSD (built by to_photoshop.jsx). Every step is its own layer,
// so any of it can be turned down or off: Render Sharpen, Focus Light, Curves, Color Balance, Grain.
// Leaves the logo layers on top, untouched. Saves the PSD and a flattened PNG next to it.
#target photoshop
(function () {
    var base = "D:/.PersonalWork/DoA/CapstoneYear4/docs/steam-store/capsule-v3/";
    var dir = base + "photoshop/";
    app.displayDialogs = DialogModes.NO;
    var doc = app.documents.getByName("main_capsule_hero_1232x706.psd");
    app.activeDocument = doc;

    function layer(name) { return doc.layers.getByName(name); }
    function placeAbove(file, name, above, mode, opacity) {
        var src = app.open(new File(dir + file));
        src.activeLayer.isBackgroundLayer = false;
        var l = src.activeLayer.duplicate(doc, ElementPlacement.PLACEATBEGINNING);
        src.close(SaveOptions.DONOTSAVECHANGES);
        app.activeDocument = doc;
        l.move(above, ElementPlacement.PLACEBEFORE);
        l.name = name; l.blendMode = mode; l.opacity = opacity;
        return l;
    }
    function adjustmentAbove(above, typeKey, typeDesc, name) {
        doc.activeLayer = above;
        var d = new ActionDescriptor(), r = new ActionReference();
        r.putClass(stringIDToTypeID("adjustmentLayer"));
        d.putReference(charIDToTypeID("null"), r);
        var u = new ActionDescriptor();
        u.putObject(charIDToTypeID("Type"), typeKey, typeDesc);
        d.putObject(charIDToTypeID("Usng"), stringIDToTypeID("adjustmentLayer"), u);
        executeAction(charIDToTypeID("Mk  "), d, DialogModes.NO);
        doc.activeLayer.name = name;
        return doc.activeLayer;
    }

    // remove an earlier polish pass, so this can be re-run
    var old = ["Render Sharpen", "Focus Light", "Curves", "Color Balance", "Grain"];
    for (var i = 0; i < old.length; i++) { try { layer(old[i]).remove(); } catch (e) {} }

    // 1. crisper detail: a sharpened copy of the render
    var render = layer("Render (Blender)");
    var sharp = render.duplicate(render, ElementPlacement.PLACEBEFORE);
    sharp.name = "Render Sharpen";
    sharp.applyUnSharpMask(70, 0.8, 2);

    // 2. a soft light on the riders and the package, to pull the eye there
    placeAbove("05_focus_light.png", "Focus Light", layer("Ground Shade"), BlendMode.SOFTLIGHT, 28);

    // 3. tone: a gentle S-curve (deeper blacks, a little more punch in the mids)
    var top = layer("Vibrance");
    var curves = adjustmentAbove(top, charIDToTypeID("Crvs"), (function () {
        var c = new ActionDescriptor();
        c.putEnumerated(stringIDToTypeID("presetKind"), stringIDToTypeID("presetKindType"), stringIDToTypeID("presetKindCustom"));
        var adjs = new ActionList(), ch = new ActionDescriptor(), cr = new ActionReference();
        cr.putEnumerated(charIDToTypeID("Chnl"), charIDToTypeID("Chnl"), charIDToTypeID("Cmps"));
        ch.putReference(charIDToTypeID("Chnl"), cr);
        var pts = [[0, 4], [52, 42], [128, 132], [200, 212], [255, 255]], pl = new ActionList();
        for (var k = 0; k < pts.length; k++) {
            var p = new ActionDescriptor();
            p.putDouble(charIDToTypeID("Hrzn"), pts[k][0]); p.putDouble(charIDToTypeID("Vrtc"), pts[k][1]);
            pl.putObject(charIDToTypeID("Pnt "), p);
        }
        ch.putList(charIDToTypeID("Crv "), pl);
        adjs.putObject(charIDToTypeID("CrvA"), ch);
        c.putList(charIDToTypeID("Adjs"), adjs);
        return c;
    })(), "Curves");

    // 4. colour: cool violet shadows, warm highlights (moonlight vs. lamp light)
    adjustmentAbove(curves, charIDToTypeID("ClrB"), (function () {
        var b = new ActionDescriptor();
        function lvl(key, r, g, bl) { var l = new ActionList(); l.putInteger(r); l.putInteger(g); l.putInteger(bl); b.putList(charIDToTypeID(key), l); }
        lvl("ShdL", -4, 0, 8);
        lvl("MdtL", 0, 0, 0);
        lvl("HghL", 6, 2, -6);
        b.putBoolean(charIDToTypeID("PrsL"), true);
        return b;
    })(), "Color Balance");

    // 5. fine film grain, to bind the render together and hide banding in the sky
    placeAbove("08_grain.png", "Grain", layer("Logo Shadow"), BlendMode.OVERLAY, 11);

    var so = new PhotoshopSaveOptions(); so.layers = true;
    doc.save();
    doc.saveAs(new File(base + "main_capsule_hero_1232x706_polished.png"), new PNGSaveOptions(), true, Extension.LOWERCASE);
    return "ok";
})();
