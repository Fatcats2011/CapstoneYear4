// Build the Library Hero (3840x1240, no logo) as a layered PSD from the PNG layers to_photoshop.py writes.
// Run by Photoshop (e.g. over COM: app.DoJavaScriptFile). Leaves the document open.
#target photoshop
(function () {
    var dir = "D:/.PersonalWork/DoA/CapstoneYear4/docs/steam-store/library-hero/photoshop/";
    var psd = "D:/.PersonalWork/DoA/CapstoneYear4/docs/steam-store/library-hero/library_hero_3840x1240.psd";
    var check = dir + "photoshop_check.png";
    app.displayDialogs = DialogModes.NO;
    app.preferences.rulerUnits = Units.PIXELS;

    var doc = app.open(new File(dir + "01_render.png"));
    doc.activeLayer.isBackgroundLayer = false;
    doc.activeLayer.name = "Render (Blender)";

    function addLayer(file, name, mode, opacity) {
        var src = app.open(new File(dir + file));
        src.activeLayer.isBackgroundLayer = false;
        var l = src.activeLayer.duplicate(doc, ElementPlacement.PLACEATBEGINNING);
        src.close(SaveOptions.DONOTSAVECHANGES);
        app.activeDocument = doc;
        l.name = name;
        if (mode) l.blendMode = mode;
        if (opacity !== undefined) l.opacity = opacity;
        return l;
    }

    addLayer("02_bloom_screen.png", "Bloom", BlendMode.SCREEN);
    addLayer("03_vignette_multiply.png", "Vignette", BlendMode.MULTIPLY);
    addLayer("04_ground_shade_multiply.png", "Ground Shade", BlendMode.MULTIPLY);

    // Adjustment layers: a touch more contrast and colour (as composite_v3.py)
    try {
        var d = new ActionDescriptor(), r = new ActionReference();
        r.putClass(stringIDToTypeID("adjustmentLayer"));
        d.putReference(charIDToTypeID("null"), r);
        var u = new ActionDescriptor(), t = new ActionDescriptor();
        t.putInteger(charIDToTypeID("Brgh"), 0);
        t.putInteger(charIDToTypeID("Cntr"), 8);
        t.putBoolean(stringIDToTypeID("useLegacy"), false);
        u.putObject(charIDToTypeID("Type"), charIDToTypeID("BrgC"), t);
        d.putObject(charIDToTypeID("Usng"), stringIDToTypeID("adjustmentLayer"), u);
        executeAction(charIDToTypeID("Mk  "), d, DialogModes.NO);
        doc.activeLayer.name = "Contrast";
    } catch (e) { $.writeln("brightness/contrast: " + e); }
    try {
        var d2 = new ActionDescriptor(), r2 = new ActionReference();
        r2.putClass(stringIDToTypeID("adjustmentLayer"));
        d2.putReference(charIDToTypeID("null"), r2);
        var u2 = new ActionDescriptor(), t2 = new ActionDescriptor();
        t2.putInteger(stringIDToTypeID("vibrance"), 10);
        t2.putInteger(charIDToTypeID("Strt"), 0);
        u2.putObject(charIDToTypeID("Type"), stringIDToTypeID("vibrance"), t2);
        d2.putObject(charIDToTypeID("Usng"), stringIDToTypeID("adjustmentLayer"), u2);
        executeAction(charIDToTypeID("Mk  "), d2, DialogModes.NO);
        doc.activeLayer.name = "Vibrance";
    } catch (e) { $.writeln("vibrance: " + e); }


    var so = new PhotoshopSaveOptions();
    so.layers = true;
    doc.saveAs(new File(psd), so, false, Extension.LOWERCASE);

    // a flattened copy so the stack can be checked against the Python composite
    var png = new PNGSaveOptions();
    doc.saveAs(new File(check), png, true, Extension.LOWERCASE);
    return "ok";
})();
