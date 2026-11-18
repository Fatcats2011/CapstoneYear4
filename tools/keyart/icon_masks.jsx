// App icon, step 1 (on the open "Untitled-2"): hide the white Background so the corners are transparent, scale the
// logo smart object to sit inside the rounded square with a margin, then export the square's and the logo's alpha
// so icon_layers.py can build the extra layers to fit them.
#target photoshop
(function () {
    var out = (typeof ICON_OUT != "undefined" ? ICON_OUT : "D:/.PersonalWork/DoA/CapstoneYear4/docs/steam-store/app-icon/") + "work/";
    var f = new Folder(out); if (!f.exists) f.create();
    var doc = app.documents.getByName(typeof ICON_DOC != "undefined" ? ICON_DOC : "Untitled-2");
    app.activeDocument = doc;
    app.preferences.rulerUnits = Units.PIXELS;
    // remove earlier enhancement layers (or copies pasted in from another icon) before measuring
    var extra = ["Depth", "Light", "Sky", "Logo Shadow", "Rim"];
    for (var x = 0; x < extra.length; x++) { try { doc.layers.getByName(extra[x]).remove(); } catch (e) {} }
    var bg = doc.layers.getByName("Background"), rect = doc.layers.getByName("Rectangle 1"), logo = doc.layers.getByName("LogoNameClean");
    bg.visible = false;

    // logo: 92% of the icon's width, centred (smart object, so scaling is lossless)
    var b = logo.bounds, w = b[2].as("px") - b[0].as("px"), h = b[3].as("px") - b[1].as("px");
    var target = doc.width.as("px") * 0.92;
    var fit = (typeof ICON_FIT != "undefined") ? ICON_FIT : 1;
    if (fit && Math.abs(w - target) > 1) logo.resize(target / w * 100, target / w * 100, AnchorPosition.MIDDLECENTER);
    b = logo.bounds;
    var cx = (b[0].as("px") + b[2].as("px")) / 2, cy = (b[1].as("px") + b[3].as("px")) / 2;
    if (fit) logo.translate(doc.width.as("px") / 2 - cx, doc.height.as("px") / 2 - cy);

    function only(layer, file) {                 // every other layer hidden (earlier enhancement layers too)
        var all = [], vis = [];
        for (var n = 0; n < doc.layers.length; n++) all.push(doc.layers[n]);
        for (var i = 0; i < all.length; i++) { vis.push(all[i].visible); all[i].visible = (all[i] == layer); }
        doc.saveAs(new File(out + file), new PNGSaveOptions(), true, Extension.LOWERCASE);
        for (var j = 0; j < all.length; j++) all[j].visible = vis[j];
    }
    only(rect, "rect.png");
    only(logo, "logo.png");
    b = logo.bounds;
    return "logo now " + b[0].as("px") + "," + b[1].as("px") + "," + b[2].as("px") + "," + b[3].as("px");
})();
