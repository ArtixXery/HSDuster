"""Firefox headless con perfil temporal limpio (sin sesión) para páginas públicas."""
import sys, time
from selenium import webdriver
def driver():
    o = webdriver.FirefoxOptions(); o.add_argument('-headless')
    return webdriver.Firefox(options=o)
if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    d = driver()
    try:
        for url in sys.argv[1:]:
            d.get(url); time.sleep(8)
            print('=====', d.title, d.current_url)
            print(d.find_element('tag name', 'body').text[:1500])
    finally:
        d.quit()
