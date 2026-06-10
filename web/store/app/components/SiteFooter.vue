<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_Footer.cshtml (footer + copyright + 到貨通知 popup).
// Class names preserved 1:1 for legacy main.css.
</script>

<template>
  <footer id="footer">
    <div class="restrict-wide">
      <div class="footertitle"><img src="/content/images/section/tfoodies-footer.png"></div>
      <ul class="foodiesword">
        <li>
          <a href="https://www.youtube.com/channel/UCACS-XffRxWsTb-a8uCWA4Q" class="circle" target="_blank" rel="nofollow"><img src="/content/images/section/youtube-footer.png"></a>
          <a href="https://www.youtube.com/channel/UCACS-XffRxWsTb-a8uCWA4Q" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/youtube.png"></a>
          <a href="https://www.youtube.com/channel/UCACS-XffRxWsTb-a8uCWA4Q" class="inline main" target="_blank" rel="nofollow">Youtube</a>
          <div class="small-content">更多好吃的健康料理教學！</div>
        </li>
        <li>
          <a href="https://cookpad.com/tw/%E4%BD%BF%E7%94%A8%E8%80%85/13374389" class="circle" target="_blank" rel="nofollow"><img src="/content/images/section/cookpad-footer.png"></a>
          <a href="https://cookpad.com/tw/%E4%BD%BF%E7%94%A8%E8%80%85/13374389" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/green.png"></a>
          <a href="https://cookpad.com/tw/%E4%BD%BF%E7%94%A8%E8%80%85/13374389" class="inline main" target="_blank" rel="nofollow">Cookpad</a>
          <div class="small-content">鎖定我們的健康食譜！</div>
        </li>
        <li>
          <a href="https://www.instagram.com/tfoodies/" class="circle" target="_blank" rel="nofollow"><img src="/content/images/section/722c0ea5891471ea726978e34046b1ed.png"></a>
          <a href="https://www.instagram.com/tfoodies/" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/instagram.png"></a>
          <a href="https://www.instagram.com/tfoodies/" class="inline main" target="_blank" rel="nofollow">Instagram</a>
          <div class="small-content">跟著我們的 IG 快速煮出美味！</div>
        </li>
        <li>
          <a href="https://www.facebook.com/trulyfoodies/" class="circle" target="_blank" rel="nofollow"><img src="/content/images/section/fb-footer.png"></a>
          <a href="https://www.facebook.com/trulyfoodies/" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/facebook.png"></a>
          <a href="https://www.facebook.com/trulyfoodies/" class="inline main" target="_blank" rel="nofollow">Facebook</a>
          <div class="small-content">Follow 食在呼尋找最真實的真食！</div>
        </li>
        <li>
          <a href="https://line.me/R/ti/p/%40kqc0569s" class="circle"><img src="/content/images/section/footer-line.png"></a>
          <a href="https://line.me/R/ti/p/%40kqc0569s" class="social-icon"><img src="/content/images/common/chat.png"></a>
          <a href="https://line.me/R/ti/p/%40kqc0569s" class="inline main">Line@</a>
          <div class="small-content">加入Line@:kqc0569s鎖定更多優惠與活動！</div>
        </li>
      </ul>
    </div>
  </footer>
  <div id="copyright">
    <div class="restrict-wide">
      <div class="copyright-wrapper">
        <div class="pull-left">
          <div class="left">
            <div class="small copyright">© 2018 COPYRIGHTS BY TFOODIES INT CO., LTD. ALL RIGHTS RESERVED.</div>
            <div class="small copyright inline"></div>
          </div>
        </div>
        <div class="pull-right">
          <div class="right">
            <ul class="copyright small">
              <li><a href="/PageMs/Contact">聯絡食在呼</a></li>
              <li><a href="/PageMs/Howtobuy">購物說明</a></li>
              <li><a href="/PageMs/Terms">網站使用條款</a></li>
              <li><a href="/PageMs/Policy">隱私權政策</a></li>
              <li><a href="/PageMs/Disclaimer">免責聲明</a></li>
            </ul>
            <ul class="copyright small">
              <li> <a href="javascript:;">(04)24366659</a> </li>
              <li> <a href="javascript:;">hi@tfoodies.com</a> </li>
              <li> <a href="javascript:;">聯絡時間：周一至周五 9:00~18:00</a> </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  </div>
  <!--
    「到貨通知」彈窗（#checkOutofnotice）暫時移除：新 Functions API 尚未移植 captcha 產生端點
    與公開的缺貨通知 POST，原本 <img src="/api/store/captcha"> 會在載入時 404。待後端補上
    （captcha + outofnotice 端點）後，從 reference/old/tfoodies/Views/Shared/_Footer.cshtml
    還原此區，並把 captcha src 與送出改走 runtimeConfig.public.apiBase。
    產品頁的「到貨通知我」按鈕（.popup-contact）目前無對應彈窗，點擊不會開窗。
  -->
</template>
