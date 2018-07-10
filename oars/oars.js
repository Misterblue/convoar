
var conversionTypes = [
    'unoptimized',
    'smallassets',
    'mergedmaterials'
];

var tableColumns = [
    'OAR file',
    'Desc']
    .Append(conversionTypes);


$(document).ready(() => {
    $.ajax(
        dataType: 'json',
        url: 'http://files.misterblue.com/BasilTest/convoar/index.json',
        success: data => {
            BuildTable(data);
        },
        error: e => {
            BuildErrorTable(e);
        }
    });
});

function BuildTable(data) {
    var tbl = document.createElement('table');
    var tr = document.createElement('tr');

    tableColumns.forEach( col => {
        var th = document.createElement('th');
        th.appendChild(document.createTextNode(col));
        tr.appendChild(th);
    });
    tbl.appenChild(tr);

    data.keys.forEach( oar => {
        tbl.appendChild(makeRow([
            makeData(function() {
                makeDiv(document.createElement(oar));
                if (oar.image) {
                    makeImg(urlBase, oar.image);
                }
            }),
            makeData(makeDiv(document.createElement('desc')]
            +
            conversionTypes.map( typ => {
                return makeDiv(
            });

        ]);
    });

    $('#tableplace').empty().append(tbl);
    $('#tableplace').empty().append(makeTable(
        [
            makeRow(tableColumns.map(col => { return makeHeader(makeText(col)); } )),

        ].concat(
            data.keys.map( oar => {
                return makeRow([
                    makeData([
                        makeDiv(makeText(oar)),

                    ],
                ].concat(
                )
                );
            });
    ));
};

function BuildErrorTable(e) {
};

function makeTable(contents, aClass) {
    return makeThing('table', contents, aClass);
};

function makeRow(contents, aClass) {
    return makeThing('tr', contents, aClass);
};

function makeHeader(contents, aClass) {
    return makeThing('th', contents, aClass);
};

function makeData(contents, aClass) {
    return makeThing('td', contents, aClass);
};

function makeDiv(contents, aClass) {
    return makeThing('div', contents, aClass);
};

function makeText(contents, aClass) {
    var tex = document.createTextNode(contents);
    if (aClass) {
        thing.setAttribute('class', aClass);
    }
    return tex;
};

function makeThing(type, contents, aClass) {
    var thing = document.createElement(type);
    if (aClass) {
        thing.setAttribute('class', aClass);
    }
    if (contents.isArray) {
        content.forEach(ent => {
            if (ent) {
                thing.appendChild(ent);
            }
        });
    }
    else {
        thing.appendChild(contents);
    }
    return thing;
}
