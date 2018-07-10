// Copyright 2018 Robert Adams
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Create OAR file display page
//
let conversionTypes = [
    'unoptimized',
    'smallassets',
    'mergedmaterials'
];

let tableColumns = [
    'OAR file',
    'Desc']
    .concat(conversionTypes);

let oarURL = 'http://files.misterblue.com/BasilTest/convoar/';

$(document).ready(() => {
    DebugLog('Fetching ' + oarURL + 'index.json');
    $.ajax({
        dataType: 'json',
        url: oarURL + 'index.json',
        success: data => {
            BuildTable(data);
        },
        error: e => {
            BuildErrorTable(e);
        }
    });
});

function LogMessage(msg, aClass) {
    if ($('#DEBUGG')) {
        if (aClass)
            $('#DEBUGG').append('<div class="' + aClass + '">' + msg + '</div');
        else
            $('#DEBUGG').append('<div>' + msg + '</div');
        if ($('#DEBUGG').children().length > 20)
            $('#DEBUGG').children('div:first').remove();
    }
};

function DebugLog(msg) {
    LogMessage(msg);
};

function ErrorLog(msg) {
    LogMesssage(msg, 'c-errorMsg');
};

function BuildTable(data) {
    let rows = [];

    let headers = [];
    tableColumns.forEach(col => {
        headers.push(makeHeader(makeText(col)));
    });
    rows.push(makeRow(headers));

    Object.keys(data).forEach( oar => {
        let cols = [];
        let firstData = [];
        firstData.push(makeDiv(makeText(oar), 'c-oarName'));
        if (data[oar].image) {
            firstData.push(makeImage(oarURL + oar + '/' + data[oar].image, 'c-oarImage'));
        }
        cols.push(makeData(firstData, 'c-col-name'));

        cols.push(makeData(makeText('desc', 'c-col-desc')));

        conversionTypes.forEach( conv => {
            if (data[oar].types.conv) {
                cols.push(makeDataSelection(data[oar].types.conv, conv, oar));
            }
            else {
                cols.push(makeData(makeText('.')));
            }
        });

        rows.push(makeRow(cols));
    });
    $('#c-tableplace').empty().append(makeTable(rows));
};

function BuildErrorTable(e) {
    $('#c-tableplace').empty().append(makeText('Could not load OAR index file'));
};

// Return a table data element containing everything about this type version of the oar
function makeDataSelection(typeDesc, type, oar) {
    let pp = [];
    if (typeDesc.gltf) {
        pp.push(makeDiv( makeButtonSmall('View', oar + '/' + type ), 'c-selection-gltf'));
    };
    if (typeDesc.tgz) {
        pp.push(makeDiv( makeURL(oarURL + oar + '/' + typeDesc.tgz, 'TGZ', 'c-selection-tgz')));
    }
    if (typeDesc.zip) {
        pp.push(makeDiv( makeURL(oarURL + oar + '/' + typeDesc.zip, 'ZIP', 'c-selection-zip')));
    }
    return makeData(pp, 'c-col-selection');
};

function makeButton(label, ref) {
    let but = document.createElement('button');
    but.setAttribute('type', 'button');
    but.setAttribute('class', 'button clickable');
    but.setAttribute('c-op', 'view');
    but.setAttribute('c-ref', ref);
    but.appendChild(makeText(label));
    return but;
}

function makeButtonSmall(label) {
};

function makeURL(url, text, aClass) {
    let anchor = document.createElement('a');
    anchor.setAttribute('href', url);
    anchor.appendChild(makeText(text));
    if (aClass) {
        anchor.setAttribute('class', aClass);
    }
    return anchor;
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

function makeImage(src, aClass) {
    let img = document.createElement('img');
    img.setAttribute('src', src);
    if (aClass) {
        img.setAttribute('class', aClass)
    }
    return img;
}

function makeText(contents) {
    let tex = document.createTextNode(contents);
    return tex;
};

function makeThing(type, contents, aClass) {
    let thing = document.createElement(type);
    if (aClass) {
        thing.setAttribute('class', aClass);
    }
    if (contents) {
        if (Array.isArray(contents)) {
            contents.forEach(ent => {
                if (typeof(ent) != 'undefined') {
                    thing.appendChild(ent);
                }
            });
        }
        else {
            thing.appendChild(contents);
        }
    }
    return thing;
};
