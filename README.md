;Developer: Pixel Tomsen (Christian Kurzhals / www.ckinfo.de) (Mail: pixel.tomsen [at] gridnet.info)
;
*
Function:

- Region AssetProxy-Module as VERY! experimental replacement for Floatsam-Cache-Module as cache-module / Gridmode !
- shares Assets for different opensim-instances via database (current mysql)
- saves simulator memory (half size!) and step down IO-Requests for server-drives
- forms a bridge to far distant asset-server (sample: EU<->US - AssetServer-requests)
- some improvement for region-crossing /attached huds or objects, if region connects to same mysql-cache-db with proxy
- temp textures (outfits, sculpts) shared for any opensim-instances, you can see all in the neighbor-sim
- lower drivespace for region-server is needed; we have a pool;-)  
*
Required: 
- mysql on a separate server (not on a region-server!) with great network-connection
*
Useless for:
- for Standalone region-server (e.g. at home)
- sqlite
- gridless region-server
* 
*
ToDo :
- copy from bin/OpenSim.Region.AssetProxy.dll to your opensim-bin-directory OR for compile: 
 
- copy this Folder to source-folder-of-opensim/addon-modules
- run runprebuild.bat (msvc) or runprebuild.sh (mono-linux)
- run compile (msvc) or xbuild (linux-mono)

after: 

- rename and setup db-connection in AssetProxy.ini.example in bin/config-include of this tree and rename this to 'AssetProxy.ini'
- add to GridCommon.ini - at Section [Modules] : 
    
    AssetCaching = "AssetProxy"
    Include-AssetProxy = "config-include/AssetProxy.ini"  

- comment out this entrys with ;:
    ;AssetCaching = "FlotsamAssetCache"
    ;Include-FlotsamCache = "config-include/FlotsamCache.ini"


Current supported Module-Console-commands after finished region-startup:
- 'assetproxy status' (shows statistics)
- 'assetproxy reset' (reset statistics)  


;
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
;