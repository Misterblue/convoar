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

    // Create row of headers
    let headers = [];
    tableColumns.forEach(col => {
        headers.push(makeHeader(col));
    });
    rows.push(makeRow(headers));

    // For each OAR file, create row of description and converted forms
    Object.keys(data).forEach( oar => {
        let cols = [];
        let firstData = [];
        firstData.push(makeDiv(oar, 'c-oarName'));
        if (data[oar].oar) {
            firstData.push(makeLink(oarURL + oar + '/' + data[oar].oar, makeButtonSmall('OAR')));
        }
        if (data[oar].image) {
            firstData.push(makeImage(oarURL + oar + '/' + data[oar].image, 'c-oarImage'));
        }
        cols.push(makeData(firstData, 'c-col-name'));

        if (data[oar].desc) {
            cols.push(makeData(makeText(data[oar].desc, 'c-col-desc')));
        }
        else {
            cols.push(makeData(makeText('.', 'c-col-desc')));
        }

        conversionTypes.forEach( conv => {
            if (data[oar].types[conv]) {
                cols.push(makeDataSelection(data[oar].types[conv], conv, oar));
            }
            else {
                cols.push(makeData('.'));
            }
        });

        rows.push(makeRow(cols));
    });
    $('#c-tableplace').empty().append(makeTable(rows, 'c-table'));
};

function BuildErrorTable(e) {
    $('#c-tableplace').empty().append(makeText('Could not load OAR index file'));
};

// Return a table data element containing everything about this type version of the oar
function makeDataSelection(typeDesc, type, oar) {
    /*
    <td class="c-col-selection">
        <div class="c-selection">
            <div class="c-viewer">
                <a href="view" target="_blank">
                    <button type="button">View</button>
                </a>
            </div>
            <div class="c-downloads">
                <div>Download:</div>
                <a href="toTGZ">
                    <button type="button">TGZ</button>
                </a>
                <a href="toZIP">
                    <button type="button">ZIP</button>
                </a>
                
            </div>
        </div>
    </td>
    */
    let selectionContents = [];

    let viewDivContents = [];
    if (typeDesc.gltf) {
        let viewURL = 'https://misterblue.com/justview/justview.html?b=' + oar + '&v=' + type;
        let viewLink = makeLink(viewURL, makeButton('View'));
        viewLink.setAttribute('target', '_blank');
        viewDivContents.push(makeDiv(viewLink, 'c-viewer'));
    }
    selectionContents.push(makeDiv(viewDivContents, 'c-viewer'));

    let downloadDivContents = [];
    downloadDivContents.push(makeDiv('Download:'));
    if (typeDesc.tgz) {
        downloadDivContents.push(makeLink(oarURL + oar + '/' + type + '/' + typeDesc.tgz, makeButtonSmall('TGZ')));
    }
    if (typeDesc.zip) {
        downloadDivContents.push(makeLink(oarURL + oar + '/' + type + '/' + typeDesc.zip, makeButtonSmall('ZIP')));
    }
    selectionContents.push(makeDiv(downloadDivContents, 'c-downloads'));

    return makeData(makeDiv(selectionContents, 'c-selection'), 'c-col-selection');
};

function makeButton(label, ref) {
    let but = makeElement('button', label, 'button clickable');
    but.setAttribute('type', 'button');
    if (ref) {
        but.setAttribute('c-op', 'view');
        but.setAttribute('c-ref', ref);
    }
    return but;
}

function makeButtonSmall(label, ref) {
    let but = makeElement('button', label, 'button-sm clickable');
    but.setAttribute('type', 'button');
    if (ref) {
        but.setAttribute('c-op', 'view');
        but.setAttribute('c-ref', ref);
    }
    return but;
};

function makeLink(url, contents, aClass) {
    let anchor = makeElement('a', contents, aClass);
    anchor.setAttribute('href', url);
    return anchor;
};

function makeTable(contents, aClass) {
    return makeElement('table', contents, aClass);
};

function makeRow(contents, aClass) {
    return makeElement('tr', contents, aClass);
};

function makeHeader(contents, aClass) {
    return makeElement('th', contents, aClass);
};

function makeData(contents, aClass) {
    return makeElement('td', contents, aClass);
};

function makeDiv(contents, aClass) {
    return makeElement('div', contents, aClass);
};

function makeImage(src, aClass) {
    let img = makeElement('img', undefined, aClass);
    img.setAttribute('src', src);
    return img;
}

function makeText(contents) {
    let tex = document.createTextNode(contents);
    return tex;
};

function makeElement(type, contents, aClass) {
    let elem = document.createElement(type);
    if (aClass) {
        elem.setAttribute('class', aClass);
    }
    if (contents) {
        if (Array.isArray(contents)) {
            contents.forEach(ent => {
                if (typeof(ent) != 'undefined') {
                    if (typeof(contents) === 'string') {
                        elem.appendChild(makeText(contents));
                    }
                    else {
                        elem.appendChild(ent);
                    }
                }
            });
        }
        else {
            if (typeof(contents) === 'string') {
                elem.appendChild(makeText(contents));
            }
            else {
                elem.appendChild(contents);
            }
        }
    }
    return elem;
};
