// App icon, step 3: add icon_layers.py's layers to the open "Untitled-2" around the user's own layers, then save a
// layered copy and a transparent PNG to docs/steam-store/app-icon/ (the open document itself is left unsaved).
#target photoshop
(function () {
    var base = typeof ICON_OUT != "undefined" ? ICON_OUT : "D:/.PersonalWork/DoA/CapstoneYear4/docs/steam-store/app-icon/";
    var work = base + "work/";
    var doc = app.documents.getByName(typeof ICON_DOC != "undefined" ? ICON_DOC : "Untitled-2");
    app.activeDocument = doc;
    var names = ["Depth", "Light", "Sky", "Logo Shadow", "Rim"];
    for (var i = 0; i < names.length; i++) { try { doc.layers.getByName(names[i]).remove(); } catch (e) {} }
    var rect = doc.layers.getByName("Rectangle 1"), logo = doc.layers.getByName("LogoNameClean");

    function place(file, name, ref, where, mode, opacity) {
        var src = app.open(new File(work + file));
        var l = src.activeLayer.duplicate(doc, ElementPlacement.PLACEATBEGINNING);
        src.close(SaveOptions.DONOTSAVECHANGES);
        app.activeDocument = doc;
        l.move(ref, where);
        l.name = name; l.blendMode = mode; l.opacity = opacity;
        return l;
    }
    var depth = place("depth.png", "Depth", rect, ElementPlacement.PLACEBEFORE, BlendMode.MULTIPLY, 70);
    var light = place("light.png", "Light", depth, ElementPlacement.PLACEBEFORE, BlendMode.SOFTLIGHT, 25);
    place("sky.png", "Sky", light, ElementPlacement.PLACEBEFORE, BlendMode.SCREEN, 90);
    place("logo_shadow.png", "Logo Shadow", logo, ElementPlacement.PLACEAFTER, BlendMode.NORMAL, 100);
    place("rim.png", "Rim", logo, ElementPlacement.PLACEBEFORE, BlendMode.NORMAL, 100);

    var so = new PhotoshopSaveOptions(); so.layers = true;
    doc.saveAs(new File(base + "app_icon_enhanced.psd"), so, true, Extension.LOWERCASE);
    var png = new PNGSaveOptions();
    doc.saveAs(new File(base + "app_icon_enhanced_" + doc.width.as("px") + ".png"), png, true, Extension.LOWERCASE);
    var order = [];
    for (var k = 0; k < doc.layers.length; k++) order.push(doc.layers[k].name);
    return order.join(" > ");
})();
